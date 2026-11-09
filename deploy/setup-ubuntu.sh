#!/usr/bin/env bash
# =====================================================================================
#  FreelanceFlow - non-Docker server setup & deploy for Ubuntu 22.04 / 24.04
# =====================================================================================
#  Run as root (sudo). Idempotent: every subcommand can be re-run safely.
#
#    sudo bash setup-ubuntu.sh [--config FILE] [--dry-run [--dry-run-root DIR]] <command> [args]
#
#  Commands
#    setup [archive.tar.gz]   install + configure everything (apt, ufw, fail2ban, .NET 9, PostgreSQL 17,
#                             nginx, certbot, systemd, backups); deploys the archive when given
#    deploy <archive.tar.gz>  new release dir -> swap `current` -> restart -> health check -> auto-rollback
#    rollback                 switch `current` back to the previous release
#    status                   service, release, health, TLS, disk, backups
#    backup                   pg_dump + files/keys snapshot now (also run daily by a systemd timer)
#    logs [-f]                last 200 API log lines (or follow)
#    render-nginx tls|http    print the nginx site this script would write (for review/testing)
#
#  Config lookup: --config FILE, else /etc/freelanceflow/freelanceflow.conf (copy kept by `setup`),
#  else freelanceflow.conf next to this script. See freelanceflow.conf.example for every key.
#  setup installs this script as /usr/local/sbin/freelanceflow, so later: `sudo freelanceflow status`.
#
#  --dry-run: no apt/systemctl/psql/ufw/...; generated files (env file, systemd units, nginx site)
#  are written under a scratch root (printed at the end) instead of /, so they can be reviewed.
# =====================================================================================
set -euo pipefail
umask 022

readonly SERVICE=freelanceflow
readonly APP_USER=freelanceflow
readonly CONF_DIR=/etc/freelanceflow
readonly ENV_FILE=/etc/freelanceflow/freelanceflow.env
readonly SERVER_CONF=/etc/freelanceflow/freelanceflow.conf
readonly CLI_PATH=/usr/local/sbin/freelanceflow
readonly NGINX_SITE=/etc/nginx/sites-available/freelanceflow
readonly ACME_ROOT=/var/www/certbot
readonly PG_VERSION=17
readonly HEALTH_TIMEOUT=120
# Body limits from Features/Images/ImageLimits.cs: admin 220 MB, portal 101 MB, intake = 3 x MaxUploadMb + 1
readonly ADMIN_BODY_MB=220
readonly PORTAL_BODY_MB=101
readonly INTAKE_MAX_IMAGES=3
# Origin allowed for writes only in HTTP-only mode, reached through `ssh -L 8080:127.0.0.1:80`
readonly TUNNEL_ORIGIN=http://localhost:8080

export DEBIAN_FRONTEND=noninteractive NEEDRESTART_MODE=a NEEDRESTART_SUSPEND=1

DRY_RUN=0
ROOT=""                 # prefix for every file this script writes/reads (dry-run scratch root)
CONFIG_ARG=""
CONFIG_FILE=""
SCRIPT_PATH="$(readlink -f "${BASH_SOURCE[0]}")"
GENERATED_DESIGNER_PW=""
GENERATED_DB_PW=0
SUMMARY_NOTES=()

# ------------------------------------------------------------------------------------- output
if [[ -t 1 ]]; then C_B=$'\e[1m'; C_G=$'\e[32m'; C_Y=$'\e[33m'; C_R=$'\e[31m'; C_0=$'\e[0m'
else C_B=""; C_G=""; C_Y=""; C_R=""; C_0=""; fi
log()  { printf '%s==>%s %s\n' "$C_B" "$C_0" "$*"; }
ok()   { printf '%s  ok%s %s\n' "$C_G" "$C_0" "$*"; }
warn() { printf '%sWARN%s %s\n' "$C_Y" "$C_0" "$*" >&2; }
die()  { printf '%sERROR%s %s\n' "$C_R" "$C_0" "$*" >&2; exit 1; }
note() { SUMMARY_NOTES+=("$*"); }

# Run a system command, or print it in dry-run mode
run() {
  if (( DRY_RUN )); then printf '  [dry-run] %s\n' "$*"; return 0; fi
  "$@"
}
# In dry-run mode, describe a system step and tell the caller to skip it: `dry_skip "..." && return 0`
dry_skip() {
  if (( DRY_RUN )); then printf '  [dry-run] skip: %s\n' "$*"; return 0; fi
  return 1
}
# Path as seen by this run (scratch root in dry-run)
p() { printf '%s%s' "$ROOT" "$1"; }

# write_file PATH MODE OWNER:GROUP  <stdin>   - atomic replace; owner ignored in dry-run
write_file() {
  local dest; dest="$(p "$1")"
  mkdir -p "$(dirname "$dest")"
  local tmp; tmp="$(mktemp "$(dirname "$dest")/.tmp.XXXXXX")"
  cat >"$tmp"
  chmod "$2" "$tmp"
  if (( ! DRY_RUN )); then chown "$3" "$tmp"; fi
  mv -f "$tmp" "$dest"
}

# ------------------------------------------------------------------------------------- config
KNOWN_KEYS=" SSH_HOST SSH_PORT SSH_USER SSH_KEY_PATH REMOTE_UPLOAD_DIR DOMAIN LETSENCRYPT_EMAIL APP_DIR FILES_DIR
 KEYS_DIR KEEP_RELEASES KESTREL_PORT LOG_LEVEL DOTNET_SERVER_GC DB_NAME DB_USER DB_PASSWORD DESIGNER_USERNAME
 DESIGNER_PASSWORD DISCORD_WEBHOOK_URL STORAGE_QUOTA_BYTES MAX_UPLOAD_MB RATE_LIMIT_LOGIN_PER_MINUTE
 RATE_LIMIT_PUBLIC_INTAKE_PER_MINUTE RATE_LIMIT_PUBLIC_INTAKE_SUBMIT_PER_HOUR
 PUBLIC_INTAKE_MAX_SUBMISSIONS_PER_IP_PER_DAY BACKUP_DIR BACKUP_RETENTION_DAYS BACKUP_TIME TIMEZONE SWAP_SIZE_MB
 JOURNAL_MAX_USE SSH_DISABLE_PASSWORD_AUTH SSH_DISABLE_ROOT_LOGIN "
KNOWN_KEYS=" ${KNOWN_KEYS//$'\n'/ } "
SECRET_KEYS=" DB_PASSWORD DESIGNER_PASSWORD DISCORD_WEBHOOK_URL "
declare -A CFG=()

# Parse KEY=value lines (no shell evaluation). Quotes are stripped; `# comment` after an unquoted value is dropped.
parse_config() {
  local file=$1 line key val n=0
  [[ -r $file ]] || die "Config file not readable: $file"
  while IFS= read -r line || [[ -n $line ]]; do
    n=$((n + 1))
    line=${line%$'\r'}
    [[ $line =~ ^[[:space:]]*(#.*)?$ ]] && continue
    [[ $line =~ ^[[:space:]]*([A-Z][A-Z0-9_]*)[[:space:]]*=[[:space:]]*(.*)$ ]] \
      || die "$file:$n: expected KEY=value, got: $line"
    key=${BASH_REMATCH[1]}
    val=${BASH_REMATCH[2]}
    if [[ $val =~ ^\"([^\"]*)\"[[:space:]]*(#.*)?$ ]] || [[ $val =~ ^\'([^\']*)\'[[:space:]]*(#.*)?$ ]]; then
      val=${BASH_REMATCH[1]}
    else
      [[ $val =~ ^([^[:space:]#]*)[[:space:]]*(#.*)?$ ]] || die "$file:$n: unquoted value of $key contains spaces"
      val=${BASH_REMATCH[1]}
    fi
    [[ $KNOWN_KEYS == *" $key "* ]] || warn "$file:$n: unknown key $key (typo?) - ignored"
    CFG[$key]=$val
  done <"$file"
}
cfg() { local v=${CFG[$1]-}; if [[ -n $v ]]; then printf '%s' "$v"; else printf '%s' "${2-}"; fi; }

is_uint()  { [[ $1 =~ ^[0-9]+$ ]]; }
is_yesno() { [[ $1 == yes || $1 == no ]]; }
# Values end up in a systemd EnvironmentFile and an nginx config: no whitespace, quotes, backslash, $, `, ;
is_safe()  { [[ $1 != *[[:space:]\"\'\\\$\`\;]* ]]; }
is_abs_path() { [[ $1 =~ ^/[A-Za-z0-9._/-]+$ && $1 != *..* && $1 != */ ]]; }

load_config() {
  if [[ -n $CONFIG_ARG ]]; then CONFIG_FILE="$(readlink -f "$CONFIG_ARG")"
  elif [[ -f "$(p "$SERVER_CONF")" ]]; then CONFIG_FILE="$(p "$SERVER_CONF")"
  elif [[ -f "$(dirname "$SCRIPT_PATH")/freelanceflow.conf" ]]; then CONFIG_FILE="$(dirname "$SCRIPT_PATH")/freelanceflow.conf"
  else die "No config: pass --config FILE (copy deploy/freelanceflow.conf.example)"; fi
  parse_config "$CONFIG_FILE"

  DOMAIN=$(cfg DOMAIN);                          LE_EMAIL=$(cfg LETSENCRYPT_EMAIL)
  APP_DIR=$(cfg APP_DIR /opt/freelanceflow/app); FILES_DIR=$(cfg FILES_DIR /var/lib/freelanceflow/files)
  KEYS_DIR=$(cfg KEYS_DIR /var/lib/freelanceflow/keys); BACKUP_DIR=$(cfg BACKUP_DIR /var/backups/freelanceflow)
  KEEP_RELEASES=$(cfg KEEP_RELEASES 5);          KESTREL_PORT=$(cfg KESTREL_PORT 5000)
  LOG_LEVEL=$(cfg LOG_LEVEL Information);        DOTNET_SERVER_GC=$(cfg DOTNET_SERVER_GC 0)
  DB_NAME=$(cfg DB_NAME freelanceflow);          DB_USER=$(cfg DB_USER ff)
  DB_PASSWORD=$(cfg DB_PASSWORD)
  DESIGNER_USERNAME=$(cfg DESIGNER_USERNAME designer); DESIGNER_PASSWORD=$(cfg DESIGNER_PASSWORD)
  DISCORD_WEBHOOK_URL=$(cfg DISCORD_WEBHOOK_URL)
  STORAGE_QUOTA_BYTES=$(cfg STORAGE_QUOTA_BYTES 10737418240); MAX_UPLOAD_MB=$(cfg MAX_UPLOAD_MB 20)
  RL_LOGIN=$(cfg RATE_LIMIT_LOGIN_PER_MINUTE 5)
  RL_INTAKE_MIN=$(cfg RATE_LIMIT_PUBLIC_INTAKE_PER_MINUTE 10)
  RL_INTAKE_HOUR=$(cfg RATE_LIMIT_PUBLIC_INTAKE_SUBMIT_PER_HOUR 5)
  INTAKE_PER_DAY=$(cfg PUBLIC_INTAKE_MAX_SUBMISSIONS_PER_IP_PER_DAY 5)
  BACKUP_RETENTION_DAYS=$(cfg BACKUP_RETENTION_DAYS 14); BACKUP_TIME=$(cfg BACKUP_TIME 02:30)
  TIMEZONE=$(cfg TIMEZONE Asia/Ho_Chi_Minh);     SWAP_SIZE_MB=$(cfg SWAP_SIZE_MB 2048)
  JOURNAL_MAX_USE=$(cfg JOURNAL_MAX_USE 1G);     SSH_PORT=$(cfg SSH_PORT 22)
  SSH_NO_PASSWORD=$(cfg SSH_DISABLE_PASSWORD_AUTH no); SSH_NO_ROOT=$(cfg SSH_DISABLE_ROOT_LOGIN no)

  [[ -n $DOMAIN ]] || die "DOMAIN is required in $CONFIG_FILE"
  [[ $DOMAIN =~ ^[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?$ ]] || die "DOMAIN is not a valid host name: $DOMAIN"
  DOMAIN=${DOMAIN,,}
  DOMAIN_IS_IP=0; [[ $DOMAIN =~ ^[0-9.]+$ ]] && DOMAIN_IS_IP=1
  if [[ -n $LE_EMAIL ]]; then
    TLS=1
    [[ $LE_EMAIL =~ ^[^@[:space:]]+@[^@[:space:]]+\.[^@[:space:]]+$ ]] || die "LETSENCRYPT_EMAIL is not an email: $LE_EMAIL"
    (( ! DOMAIN_IS_IP )) || die "Let's Encrypt needs a DNS name, not an IP (DOMAIN=$DOMAIN). Leave LETSENCRYPT_EMAIL empty for HTTP-only test mode."
  else
    TLS=0
  fi
  local v
  for v in APP_DIR FILES_DIR KEYS_DIR BACKUP_DIR; do is_abs_path "${!v}" || die "$v must be an absolute path without spaces: ${!v}"; done
  for v in KEEP_RELEASES KESTREL_PORT STORAGE_QUOTA_BYTES MAX_UPLOAD_MB RL_LOGIN RL_INTAKE_MIN RL_INTAKE_HOUR \
           INTAKE_PER_DAY BACKUP_RETENTION_DAYS SWAP_SIZE_MB SSH_PORT; do
    is_uint "${!v}" || die "$v must be a whole number: ${!v}"
  done
  (( KEEP_RELEASES >= 2 )) || die "KEEP_RELEASES must be >= 2 (rollback needs a previous release)"
  (( KESTREL_PORT > 1023 && KESTREL_PORT < 65536 )) || die "KESTREL_PORT must be 1024-65535"
  (( MAX_UPLOAD_MB >= 1 && MAX_UPLOAD_MB <= 200 )) || die "MAX_UPLOAD_MB must be 1-200"
  (( BACKUP_RETENTION_DAYS >= 1 )) || die "BACKUP_RETENTION_DAYS must be >= 1"
  [[ $BACKUP_TIME =~ ^([01][0-9]|2[0-3]):[0-5][0-9]$ ]] || die "BACKUP_TIME must be HH:MM"
  [[ $LOG_LEVEL =~ ^(Verbose|Debug|Information|Warning|Error|Fatal)$ ]] || die "LOG_LEVEL invalid: $LOG_LEVEL"
  [[ $DOTNET_SERVER_GC == 0 || $DOTNET_SERVER_GC == 1 ]] || die "DOTNET_SERVER_GC must be 0 or 1"
  [[ $DB_NAME =~ ^[a-z_][a-z0-9_]{0,62}$ ]] || die "DB_NAME must be lower-case letters, digits, _"
  [[ $DB_USER =~ ^[a-z_][a-z0-9_]{0,62}$ ]] || die "DB_USER must be lower-case letters, digits, _"
  [[ -z $DB_PASSWORD || $DB_PASSWORD =~ ^[A-Za-z0-9._~-]{16,}$ ]] || die "DB_PASSWORD: at least 16 chars of A-Z a-z 0-9 . _ ~ - (or leave empty to generate)"
  [[ $DESIGNER_USERNAME =~ ^[A-Za-z0-9._@+-]{3,100}$ ]] || die "DESIGNER_USERNAME invalid"
  [[ -z $DESIGNER_PASSWORD ]] || { is_safe "$DESIGNER_PASSWORD" && (( ${#DESIGNER_PASSWORD} >= 12 )); } \
    || die "DESIGNER_PASSWORD: at least 12 chars, no spaces/quotes/backslash/\$/\`/; (or leave empty to generate)"
  if [[ -n $DISCORD_WEBHOOK_URL && $DISCORD_WEBHOOK_URL != none ]]; then
    if [[ $DISCORD_WEBHOOK_URL != https://* ]] || ! is_safe "$DISCORD_WEBHOOK_URL"; then
      die "DISCORD_WEBHOOK_URL must be an https:// URL, empty, or none"
    fi
  fi
  [[ $JOURNAL_MAX_USE =~ ^[0-9]+[KMG]$ ]] || die "JOURNAL_MAX_USE must look like 500M or 1G"
  [[ $TIMEZONE =~ ^[A-Za-z0-9_+/-]+$ ]] || die "TIMEZONE invalid: $TIMEZONE"
  is_yesno "$SSH_NO_PASSWORD" || die "SSH_DISABLE_PASSWORD_AUTH must be yes or no"
  is_yesno "$SSH_NO_ROOT" || die "SSH_DISABLE_ROOT_LOGIN must be yes or no"

  RELEASES_DIR=$APP_DIR/releases
  CURRENT=$APP_DIR/current
  local intake=$(( MAX_UPLOAD_MB * INTAKE_MAX_IMAGES + 1 )) max=$ADMIN_BODY_MB
  (( intake > max )) && max=$intake
  (( PORTAL_BODY_MB > max )) && max=$PORTAL_BODY_MB
  API_BODY_MB=$(( max + 5 ))
  if (( TLS )); then PUBLIC_URL="https://$DOMAIN"; else PUBLIC_URL="http://$DOMAIN"; fi
}

# ------------------------------------------------------------------------------------- preflight
require_root() {
  (( DRY_RUN )) && return 0
  [[ $EUID -eq 0 ]] || die "Run as root: sudo bash $0 $*"
}

check_os() {
  [[ -r /etc/os-release ]] || die "Cannot read /etc/os-release"
  # shellcheck disable=SC1091
  . /etc/os-release
  [[ ${ID:-} == ubuntu ]] || die "Only Ubuntu is supported (found ${ID:-unknown})"
  case ${VERSION_ID:-} in
    22.04|24.04) OS_VERSION=$VERSION_ID; OS_CODENAME=$VERSION_CODENAME ;;
    *) die "Only Ubuntu 22.04 and 24.04 are supported (found ${VERSION_ID:-unknown})" ;;
  esac
  [[ $(uname -m) == x86_64 ]] || die "Only x86_64 is supported (the Windows build publishes linux-x64)"
}

# ------------------------------------------------------------------------------------- secrets / env file
gen_hex()      { openssl rand -hex "$1"; }
gen_password() { openssl rand -base64 24 | tr -d '/+=\n'; }

# env_get FILE KEY  - value of KEY in a KEY=VALUE file (empty if missing)
env_get() {
  local file=$1 key=$2 line
  [[ -f $file ]] || return 0
  while IFS= read -r line; do
    if [[ $line == "$key="* ]]; then printf '%s' "${line#*=}"; return 0; fi
  done <"$file"
}

db_port() {
  local port=""
  if (( ! DRY_RUN )) && command -v pg_lsclusters >/dev/null; then
    port=$(pg_lsclusters -h 2>/dev/null | awk -v v="$PG_VERSION" '$1==v && $2=="main" {print $3}')
  fi
  printf '%s' "${port:-5432}"
}

# psql as the postgres superuser over the local socket (peer auth: no password involved)
pg() { runuser -u postgres -- psql -X -v ON_ERROR_STOP=1 -q "$@"; }

designer_seeded() {
  (( DRY_RUN )) && return 1
  local n
  n=$(pg -d "$DB_NAME" -tAc "SELECT count(*) FROM designer_accounts" 2>/dev/null || true)
  [[ ${n:-0} =~ ^[1-9] ]]
}

write_env_file() {
  log "Writing $ENV_FILE"
  local envf; envf="$(p "$ENV_FILE")"
  local old_cs old_pw="" db_pw seed_pw="" hook
  old_cs=$(env_get "$envf" ConnectionStrings__DefaultConnection)
  [[ $old_cs =~ (^|;)Password=([^;]*) ]] && old_pw=${BASH_REMATCH[2]}
  if [[ -n $old_pw ]]; then
    db_pw=$old_pw
    [[ -z $DB_PASSWORD || $DB_PASSWORD == "$old_pw" ]] \
      || warn "DB_PASSWORD in the config differs from the one already in $ENV_FILE - keeping the existing one (never overwritten)"
  elif [[ -n $DB_PASSWORD ]]; then
    db_pw=$DB_PASSWORD
  else
    db_pw=$(gen_hex 24); GENERATED_DB_PW=1
  fi
  DB_PW=$db_pw

  case $DISCORD_WEBHOOK_URL in
    none) hook="" ;;
    "")   hook=$(env_get "$envf" Notifications__Discord__WebhookUrl) ;;
    *)    hook=$DISCORD_WEBHOOK_URL ;;
  esac

  local seeded=0
  designer_seeded && seeded=1
  if (( ! seeded )); then
    if [[ -n $DESIGNER_PASSWORD ]]; then seed_pw=$DESIGNER_PASSWORD
    else
      seed_pw=$(env_get "$envf" Seed__DesignerPassword)
      if [[ -z $seed_pw ]]; then seed_pw=$(gen_password); GENERATED_DESIGNER_PW=$seed_pw; fi
    fi
  fi

  {
    echo "# Generated by $CLI_PATH setup on $(date -Is). Secrets live ONLY here - do not copy this file around."
    echo "# Change settings in $SERVER_CONF (or your local freelanceflow.conf) and re-run setup."
    echo "ASPNETCORE_ENVIRONMENT=Production"
    echo "ASPNETCORE_URLS=http://127.0.0.1:$KESTREL_PORT"
    echo "ConnectionStrings__DefaultConnection=Host=127.0.0.1;Port=$(db_port);Database=$DB_NAME;Username=$DB_USER;Password=$db_pw"
    echo "App__PublicBaseUrl=https://$DOMAIN"
    (( TLS )) || echo "App__AdditionalOrigins__0=$TUNNEL_ORIGIN"
    echo "App__MaxUploadMb=$MAX_UPLOAD_MB"
    echo "Storage__Local__RootPath=$FILES_DIR"
    echo "Storage__QuotaBytes=$STORAGE_QUOTA_BYTES"
    echo "DataProtection__KeysPath=$KEYS_DIR"
    echo "Notifications__Discord__WebhookUrl=$hook"
    echo "RateLimit__LoginPerMinute=$RL_LOGIN"
    echo "RateLimit__PublicIntakePerMinute=$RL_INTAKE_MIN"
    echo "RateLimit__PublicIntakeSubmitPerHour=$RL_INTAKE_HOUR"
    echo "PublicIntake__MaxSubmissionsPerIpPerDay=$INTAKE_PER_DAY"
    echo "Serilog__MinimumLevel__Default=$LOG_LEVEL"
    echo "DOTNET_gcServer=$DOTNET_SERVER_GC"
    echo "DOTNET_CLI_TELEMETRY_OPTOUT=1"
    echo "DOTNET_NOLOGO=1"
    echo "TZ=$TIMEZONE"
    if (( ! seeded )); then
      echo "# Removed automatically once the designer account exists"
      echo "Seed__DesignerUsername=$DESIGNER_USERNAME"
      echo "Seed__DesignerPassword=$seed_pw"
    fi
  } | write_file "$ENV_FILE" 600 "root:$APP_USER"
  ok "env file written (mode 600, root:$APP_USER)"
}

# After a healthy start the seed has run: drop the initial password from the env file
strip_seed_from_env() {
  local envf; envf="$(p "$ENV_FILE")"
  [[ -f $envf ]] || return 0
  grep -q '^Seed__' "$envf" || return 0
  designer_seeded || return 0
  grep -v -e '^Seed__' -e '^# Removed automatically once the designer account exists' "$envf" \
    | write_file "$ENV_FILE" 600 "root:$APP_USER"
  ok "designer account exists - removed Seed__* from $ENV_FILE"
}

# Keep a copy of the config on the server for `freelanceflow deploy/status/backup` - secrets blanked
save_server_config() {
  local dest; dest="$(p "$SERVER_CONF")"
  if [[ -f $dest && "$(readlink -f "$dest")" == "$CONFIG_FILE" ]]; then return 0; fi
  local line key
  while IFS= read -r line || [[ -n $line ]]; do
    line=${line%$'\r'}
    if [[ $line =~ ^[[:space:]]*([A-Z][A-Z0-9_]*)[[:space:]]*= ]]; then
      key=${BASH_REMATCH[1]}
      if [[ $SECRET_KEYS == *" $key "* ]]; then
        printf '%s=\n' "$key"
        continue
      fi
    fi
    printf '%s\n' "$line"
  done <"$CONFIG_FILE" | write_file "$SERVER_CONF" 600 root:root
  ok "config copy saved to $SERVER_CONF (secrets blanked; they live in $ENV_FILE)"
}

install_cli() {
  if [[ "$SCRIPT_PATH" == "$(p "$CLI_PATH")" ]]; then return 0; fi
  write_file "$CLI_PATH" 750 root:root <"$SCRIPT_PATH"
  ok "installed $CLI_PATH  (use: sudo freelanceflow status|deploy|backup|rollback|logs)"
}

# ------------------------------------------------------------------------------------- system
# Lock timeout: on a fresh VM cloud-init / unattended-upgrades often hold the dpkg lock for a while
APT_OPTS=(-y -q -o DPkg::Lock::Timeout=600 -o Dpkg::Options::=--force-confdef -o Dpkg::Options::=--force-confold)
apt_install() { run apt-get install "${APT_OPTS[@]}" "$@"; }
apt_update()  { run apt-get update -q -o DPkg::Lock::Timeout=600; }

system_packages() {
  log "Updating Ubuntu $OS_VERSION and installing base packages"
  apt_update
  run apt-get upgrade "${APT_OPTS[@]}"
  apt_install ca-certificates curl gnupg lsb-release software-properties-common openssl rsync tar gzip \
    ufw fail2ban python3-systemd unattended-upgrades nginx certbot
}

setup_timezone() {
  log "Timezone $TIMEZONE"
  dry_skip "timedatectl set-timezone $TIMEZONE" && return 0
  [[ -e /usr/share/zoneinfo/$TIMEZONE ]] || die "Unknown TIMEZONE: $TIMEZONE (see: timedatectl list-timezones)"
  timedatectl set-timezone "$TIMEZONE" 2>/dev/null || ln -sf "/usr/share/zoneinfo/$TIMEZONE" /etc/localtime
}

setup_swap() {
  (( SWAP_SIZE_MB > 0 )) || return 0
  dry_skip "swapfile ${SWAP_SIZE_MB} MB if no swap and RAM < 4 GB" && return 0
  local mem_mb; mem_mb=$(awk '/^MemTotal:/ {print int($2/1024)}' /proc/meminfo)
  if [[ -n "$(swapon --noheadings 2>/dev/null)" ]]; then ok "swap already active"; return 0; fi
  if (( mem_mb >= 4096 )); then ok "RAM ${mem_mb} MB >= 4 GB - no swap needed"; return 0; fi
  log "Creating ${SWAP_SIZE_MB} MB swapfile (RAM ${mem_mb} MB)"
  if [[ ! -f /swapfile ]]; then
    fallocate -l "${SWAP_SIZE_MB}M" /swapfile 2>/dev/null || dd if=/dev/zero of=/swapfile bs=1M count="$SWAP_SIZE_MB" status=none
    chmod 600 /swapfile
    mkswap /swapfile >/dev/null
  fi
  swapon /swapfile
  grep -q '^/swapfile ' /etc/fstab || echo '/swapfile none swap sw 0 0' >>/etc/fstab
  echo 'vm.swappiness=10' >/etc/sysctl.d/99-freelanceflow-swap.conf
  sysctl -q -p /etc/sysctl.d/99-freelanceflow-swap.conf
  ok "swap on"
}

setup_journald() {
  printf '[Journal]\nSystemMaxUse=%s\n' "$JOURNAL_MAX_USE" | write_file /etc/systemd/journald.conf.d/90-freelanceflow.conf 644 root:root
  run systemctl restart systemd-journald
}

ssh_ports() {
  local ports="$SSH_PORT"
  if (( ! DRY_RUN )) && command -v sshd >/dev/null; then
    ports="$ports $(sshd -T 2>/dev/null | awk '$1=="port" {print $2}' | tr '\n' ' ')"
  fi
  printf '%s\n' "$ports" | tr ' ' '\n' | grep -E '^[0-9]+$' | sort -un | tr '\n' ' '
}

setup_firewall() {
  log "Firewall (ufw): SSH, 80, 443 only"
  local port
  run ufw default deny incoming
  run ufw default allow outgoing
  for port in $(ssh_ports); do run ufw allow "$port/tcp" comment 'SSH'; done
  run ufw allow 80/tcp comment 'HTTP'
  run ufw allow 443/tcp comment 'HTTPS'
  run ufw --force enable
}

setup_fail2ban() {
  log "fail2ban (sshd jail)"
  local ports; ports=$(ssh_ports); ports=${ports% }; ports=${ports// /,}
  printf '[sshd]\nenabled = true\nport = %s\nbackend = systemd\nmaxretry = 5\nfindtime = 10m\nbantime = 1h\n' "$ports" \
    | write_file /etc/fail2ban/jail.d/90-freelanceflow.local 644 root:root
  run systemctl enable fail2ban
  run systemctl restart fail2ban
}

setup_unattended_upgrades() {
  log "Unattended security upgrades"
  printf 'APT::Periodic::Update-Package-Lists "1";\nAPT::Periodic::Unattended-Upgrade "1";\nAPT::Periodic::AutocleanInterval "7";\n' \
    | write_file /etc/apt/apt.conf.d/20auto-upgrades 644 root:root
  run systemctl enable --now unattended-upgrades
}

setup_ssh_hardening() {
  local conf=/etc/ssh/sshd_config.d/01-freelanceflow.conf
  if [[ $SSH_NO_PASSWORD == no && $SSH_NO_ROOT == no ]]; then
    if [[ -f "$(p "$conf")" ]]; then
      run rm -f "$conf"; run systemctl reload ssh
      ok "SSH hardening removed (both toggles are off)"
    fi
    return 0
  fi
  log "SSH hardening"
  local who=${SUDO_USER:-root} home
  if [[ $SSH_NO_PASSWORD == yes ]] && (( ! DRY_RUN )); then
    home=$(getent passwd "$who" | cut -d: -f6)
    [[ -s $home/.ssh/authorized_keys ]] || die "SSH_DISABLE_PASSWORD_AUTH=yes but $who has no $home/.ssh/authorized_keys - you would be locked out"
  fi
  if [[ $SSH_NO_ROOT == yes && $who == root ]] && (( ! DRY_RUN )); then
    die "SSH_DISABLE_ROOT_LOGIN=yes but you are running as root - log in as a sudo user first"
  fi
  {
    echo "# Managed by $CLI_PATH setup (01- sorts before cloud-init's 50-: first value wins in sshd)"
    if [[ $SSH_NO_PASSWORD == yes ]]; then
      echo "PasswordAuthentication no"; echo "KbdInteractiveAuthentication no"
    fi
    if [[ $SSH_NO_ROOT == yes ]]; then echo "PermitRootLogin no"; fi
  } | write_file "$conf" 644 root:root
  if (( ! DRY_RUN )); then
    sshd -t || { rm -f "$conf"; die "sshd rejected the hardening config - removed it"; }
    systemctl reload ssh 2>/dev/null || systemctl restart ssh
  fi
  ok "sshd: password auth=$([[ $SSH_NO_PASSWORD == yes ]] && echo off || echo on), root login=$([[ $SSH_NO_ROOT == yes ]] && echo off || echo on)"
}

# ------------------------------------------------------------------------------------- .NET 9
# (no `| grep -q`: an early grep exit would SIGPIPE the producer and fail the pipeline under pipefail)
has_aspnet9() {
  command -v dotnet >/dev/null || return 1
  local rt; rt=$(dotnet --list-runtimes 2>/dev/null || true)
  [[ $rt == *"Microsoft.AspNetCore.App 9."* ]]
}

install_dotnet() {
  log "ASP.NET Core 9 runtime"
  dry_skip "ppa:dotnet/backports -> apt install aspnetcore-runtime-9.0 (fallback: dotnet-install.sh)" && { DOTNET_BIN=/usr/bin/dotnet; return 0; }
  if ! has_aspnet9; then
    # Neither 22.04 nor 24.04 ships .NET 9 in its own archive (only 8.0); Canonical's backports PPA carries it
    # for both, and apt keeps it patched. Fallback: Microsoft's dotnet-install.sh (no automatic updates).
    if add-apt-repository -y ppa:dotnet/backports && apt_update && apt_install aspnetcore-runtime-9.0; then
      ok "aspnetcore-runtime-9.0 from ppa:dotnet/backports"
    else
      warn "PPA install failed - falling back to dotnet-install.sh into /usr/share/dotnet (re-run setup to update it)"
      local icu=libicu70; [[ $OS_VERSION == 24.04 ]] && icu=libicu74
      apt_install "$icu" libssl3 zlib1g ca-certificates
      local tmp; tmp=$(mktemp)
      curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$tmp"
      bash "$tmp" --channel 9.0 --runtime aspnetcore --install-dir /usr/share/dotnet
      rm -f "$tmp"
      [[ -e /usr/bin/dotnet ]] || ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
    fi
  fi
  has_aspnet9 || die "ASP.NET Core 9 runtime still missing"
  DOTNET_BIN=$(command -v dotnet)
  ok "$(dotnet --list-runtimes | grep '^Microsoft.AspNetCore.App 9\.' | tail -n 1)"
}

# ------------------------------------------------------------------------------------- PostgreSQL 17
install_postgres() {
  log "PostgreSQL $PG_VERSION (PGDG apt repo)"
  dry_skip "PGDG key in /etc/apt/keyrings, apt install postgresql-$PG_VERSION, listen localhost" && return 0
  install -d -m 0755 /etc/apt/keyrings
  if [[ ! -s /etc/apt/keyrings/postgresql.gpg ]]; then
    curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc | gpg --dearmor --yes -o /etc/apt/keyrings/postgresql.gpg
    chmod 644 /etc/apt/keyrings/postgresql.gpg
  fi
  echo "deb [signed-by=/etc/apt/keyrings/postgresql.gpg] https://apt.postgresql.org/pub/repos/apt ${OS_CODENAME}-pgdg main" \
    >/etc/apt/sources.list.d/pgdg.list
  apt_update
  apt_install "postgresql-$PG_VERSION" "postgresql-client-$PG_VERSION"
  [[ -n "$(pg_lsclusters -h | awk -v v="$PG_VERSION" '$1==v && $2=="main"')" ]] \
    || pg_createcluster "$PG_VERSION" main --start
  local dropin=/etc/postgresql/$PG_VERSION/main/conf.d/90-freelanceflow.conf
  local want="# Managed by $CLI_PATH setup: never reachable from the network
listen_addresses = 'localhost'"
  if [[ "$(cat "$dropin" 2>/dev/null)" != "$want" ]]; then
    install -d -o postgres -g postgres "$(dirname "$dropin")"
    printf '%s\n' "$want" >"$dropin"
    chown postgres:postgres "$dropin"
    systemctl restart "postgresql@${PG_VERSION}-main"
  fi
  systemctl enable --now "postgresql@${PG_VERSION}-main" >/dev/null 2>&1 || systemctl enable --now postgresql
  local i
  for i in $(seq 1 30); do runuser -u postgres -- pg_isready -q && break; sleep 1; done
  runuser -u postgres -- pg_isready -q || die "PostgreSQL did not become ready"
  ok "PostgreSQL $PG_VERSION running on 127.0.0.1:$(db_port)"
}

setup_database() {
  log "Database role '$DB_USER' and database '$DB_NAME'"
  dry_skip "CREATE ROLE/ALTER ROLE password, CREATE DATABASE, CREATE EXTENSION unaccent" && return 0
  # SQL goes through stdin (not argv), so the password never shows up in `ps`
  pg <<SQL
DO \$\$ BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '$DB_USER') THEN CREATE ROLE "$DB_USER" LOGIN; END IF;
END \$\$;
ALTER ROLE "$DB_USER" WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD '$DB_PW';
SQL
  if [[ "$(pg -tAc "SELECT 1 FROM pg_database WHERE datname = '$DB_NAME'")" != 1 ]]; then
    pg -c "CREATE DATABASE \"$DB_NAME\" OWNER \"$DB_USER\" ENCODING 'UTF8' TEMPLATE template0"
  fi
  # Migration AddUnaccentExtension also runs CREATE EXTENSION IF NOT EXISTS - creating it here first is harmless
  pg -d "$DB_NAME" -c "CREATE EXTENSION IF NOT EXISTS unaccent"
  ok "role, database and unaccent ready"
}

# ------------------------------------------------------------------------------------- user, dirs, systemd
setup_user_dirs() {
  log "System user '$APP_USER' and directories"
  if (( ! DRY_RUN )) && ! id -u "$APP_USER" >/dev/null 2>&1; then
    useradd --system --user-group --home-dir /var/lib/freelanceflow --no-create-home --shell /usr/sbin/nologin "$APP_USER"
  fi
  if (( DRY_RUN )); then
    mkdir -p "$(p "$RELEASES_DIR")" "$(p "$FILES_DIR")" "$(p "$KEYS_DIR")" "$(p "$BACKUP_DIR")" "$(p "$CONF_DIR")" "$(p "$ACME_ROOT")"
    return 0
  fi
  install -d -m 0755 -o root -g root "$(dirname "$APP_DIR")" "$APP_DIR" "$RELEASES_DIR" "$ACME_ROOT"
  install -d -m 0755 -o root -g root "$(dirname "$FILES_DIR")"
  [[ $(dirname "$KEYS_DIR") == "$(dirname "$FILES_DIR")" ]] || install -d -m 0755 -o root -g root "$(dirname "$KEYS_DIR")"
  install -d -m 0750 -o "$APP_USER" -g "$APP_USER" "$FILES_DIR"
  install -d -m 0700 -o "$APP_USER" -g "$APP_USER" "$KEYS_DIR"
  install -d -m 0700 -o root -g root "$BACKUP_DIR" "$BACKUP_DIR/db" "$BACKUP_DIR/files"
  install -d -m 0750 -o root -g "$APP_USER" "$CONF_DIR"
  # existing data (e.g. restored from a backup) must stay writable by the app user
  chown -R "$APP_USER:$APP_USER" "$FILES_DIR" "$KEYS_DIR"
  ok "$APP_DIR, $FILES_DIR, $KEYS_DIR, $BACKUP_DIR"
}

write_systemd_units() {
  log "systemd units"
  local dotnet=${DOTNET_BIN:-/usr/bin/dotnet}
  write_file "/etc/systemd/system/$SERVICE.service" 644 root:root <<UNIT
# Managed by $CLI_PATH setup - edit freelanceflow.conf and re-run setup instead.
[Unit]
Description=FreelanceFlow API (ASP.NET Core, Kestrel on 127.0.0.1:$KESTREL_PORT behind nginx)
After=network-online.target postgresql.service
Wants=network-online.target postgresql.service

[Service]
Type=simple
User=$APP_USER
Group=$APP_USER
WorkingDirectory=$CURRENT/app
ExecStart=$dotnet $CURRENT/app/FreelanceFlow.Api.dll
EnvironmentFile=$ENV_FILE
Restart=always
RestartSec=5
TimeoutStopSec=30
SyslogIdentifier=$SERVICE
UMask=0027
LimitNOFILE=65536

# Hardening: read-only system, writable only the image store and the key ring
NoNewPrivileges=true
PrivateTmp=true
PrivateDevices=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=$FILES_DIR $KEYS_DIR
ProtectKernelTunables=true
ProtectKernelModules=true
ProtectKernelLogs=true
ProtectControlGroups=true
ProtectClock=true
ProtectHostname=true
ProtectProc=invisible
RestrictSUIDSGID=true
RestrictRealtime=true
RestrictNamespaces=true
LockPersonality=true
RestrictAddressFamilies=AF_INET AF_INET6 AF_UNIX
CapabilityBoundingSet=
AmbientCapabilities=
SystemCallArchitectures=native
# No MemoryDenyWriteExecute: the .NET JIT needs writable+executable memory

[Install]
WantedBy=multi-user.target
UNIT

  local h=${BACKUP_TIME%:*} m=${BACKUP_TIME#*:}
  write_file "/etc/systemd/system/$SERVICE-backup.service" 644 root:root <<UNIT
[Unit]
Description=FreelanceFlow daily backup (pg_dump + files/keys snapshot)
After=postgresql.service

[Service]
Type=oneshot
ExecStart=$CLI_PATH backup
Nice=10
IOSchedulingClass=idle
UNIT
  write_file "/etc/systemd/system/$SERVICE-backup.timer" 644 root:root <<UNIT
[Unit]
Description=Daily FreelanceFlow backup at $BACKUP_TIME

[Timer]
OnCalendar=*-*-* $h:$m:00
Persistent=true
RandomizedDelaySec=5min

[Install]
WantedBy=timers.target
UNIT
  run systemctl daemon-reload
  run systemctl enable "$SERVICE.service"
  run systemctl enable --now "$SERVICE-backup.timer"
  ok "$SERVICE.service, $SERVICE-backup.timer ($BACKUP_TIME daily)"
}

# ------------------------------------------------------------------------------------- nginx
cert_dir() { printf '/etc/letsencrypt/live/%s' "$DOMAIN"; }

nginx_ipv6() {
  case ${FF_NGINX_IPV6:-auto} in
    yes) return 0 ;; no) return 1 ;;
    *) [[ -s /proc/net/if_inet6 ]] ;;
  esac
}

# nginx >= 1.25.1 wants `http2 on;` (listen ... http2 is deprecated); Ubuntu 22.04 has 1.18, 24.04 has 1.24
nginx_http2_directive() {
  case ${FF_NGINX_HTTP2:-auto} in
    directive) return 0 ;; listen) return 1 ;;
  esac
  local v; v=$(nginx -v 2>&1 || true); v=${v##*nginx/}; v=${v%% *}
  [[ $v =~ ^([0-9]+)\.([0-9]+)\.([0-9]+) ]] || return 1
  local maj=${BASH_REMATCH[1]} min=${BASH_REMATCH[2]} pat=${BASH_REMATCH[3]}
  (( maj > 1 || min > 25 || (min == 25 && pat >= 1) ))
}

# Security headers of deploy/Caddyfile (07-security s6). nginx drops server-level add_header in any location
# that has its own add_header, so every such location repeats the full set through this function.
# nginx_headers INDENT REFERRER_POLICY
nginx_headers() {
  local i=$1 ref=$2
  (( RENDER_TLS )) && echo "${i}add_header Strict-Transport-Security \"max-age=31536000; includeSubDomains\" always;"
  cat <<EOF
${i}add_header X-Content-Type-Options nosniff always;
${i}add_header X-Frame-Options DENY always;
${i}add_header Permissions-Policy "camera=(), microphone=(), geolocation=()" always;
${i}add_header Content-Security-Policy "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob: https:; font-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'" always;
${i}add_header Referrer-Policy $ref always;
EOF
}

nginx_proxy() {
  local i=$1
  cat <<EOF
${i}proxy_pass http://freelanceflow_api;
${i}proxy_http_version 1.1;
${i}proxy_set_header Connection "";
${i}proxy_set_header Host \$host;
${i}# \$remote_addr, never \$proxy_add_x_forwarded_for: the API trusts exactly one hop (ForwardLimit=1,
${i}# KnownProxies cleared), so a client-supplied X-Forwarded-For must not reach it.
${i}proxy_set_header X-Forwarded-For \$remote_addr;
${i}proxy_set_header X-Forwarded-Proto \$scheme;
${i}# Uploads: admin 10 x 20 MB (220 MB), portal 101 MB, public intake 3 x MAX_UPLOAD_MB + 1 MB
${i}client_max_body_size ${API_BODY_MB}m;
${i}client_body_timeout 120s;
${i}proxy_connect_timeout 10s;
${i}proxy_send_timeout 300s;
${i}proxy_read_timeout 300s;
EOF
}

# render_nginx tls|http  -> full site config on stdout
render_nginx() {
  local mode=$1 l80="listen 80 default_server;" l80v6="" h2=" http2" h2line="" l443 l443v6=""
  RENDER_TLS=0; [[ $mode == tls ]] && RENDER_TLS=1
  if nginx_http2_directive; then h2=""; h2line="http2 on;"; fi
  l443="listen 443 ssl$h2 default_server;"
  if nginx_ipv6; then
    l80v6="listen [::]:80 default_server;"; l443v6="listen [::]:443 ssl$h2 default_server;"
  fi
  cat <<EOF
# FreelanceFlow - generated by $CLI_PATH setup ($mode mode). Do not edit: change freelanceflow.conf and re-run setup.
# Equivalent of deploy/Caddyfile for the non-Docker deployment.

upstream freelanceflow_api {
    # Kestrel listens on loopback only (ASPNETCORE_URLS); see the ForwardedHeaders note in Program.cs
    server 127.0.0.1:$KESTREL_PORT;
    keepalive 16;
}

EOF
  if (( RENDER_TLS )); then
    cat <<EOF
server {
    $l80
    ${l80v6:+$l80v6
    }server_name $DOMAIN;
    server_tokens off;

    location ^~ /.well-known/acme-challenge/ {
        root $ACME_ROOT;
        default_type text/plain;
    }
    location / {
        return 301 https://$DOMAIN\$request_uri;
    }
}

server {
    $l443
    ${l443v6:+$l443v6
    }${h2line:+$h2line
    }server_name $DOMAIN;

    ssl_certificate     $(cert_dir)/fullchain.pem;
    ssl_certificate_key $(cert_dir)/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_prefer_server_ciphers off;
    ssl_session_cache shared:FreelanceFlowTLS:10m;
    ssl_session_timeout 1d;
    ssl_session_tickets off;
EOF
  else
    cat <<EOF
# HTTP-ONLY TEST MODE (LETSENCRYPT_EMAIL empty): no TLS, no HSTS. Writes from http://$DOMAIN get 403
# (the CSRF cookie is Secure); a full test works through  ssh -L 8080:127.0.0.1:80  ->  $TUNNEL_ORIGIN
server {
    $l80
    ${l80v6:+$l80v6
    }server_name $DOMAIN;

    location ^~ /.well-known/acme-challenge/ {
        root $ACME_ROOT;
        default_type text/plain;
    }
EOF
  fi
  cat <<EOF

    server_tokens off;
    root $CURRENT/web;
    index index.html;
    charset utf-8;
    client_max_body_size 1m;

    gzip on;
    gzip_vary on;
    gzip_proxied any;
    gzip_comp_level 5;
    gzip_min_length 1024;
    gzip_types text/plain text/css text/javascript application/javascript application/json application/manifest+json image/svg+xml;

    # Site-wide headers (apply to every location below that has no add_header of its own)
$(nginx_headers "    " strict-origin-when-cross-origin)

    # Public intake API: the path carries the form slug - keep it out of the access log (07-security s4.5)
    location ^~ /api/public/ {
        access_log off;
$(nginx_proxy "        ")
$(nginx_headers "        " strict-origin-when-cross-origin)
    }

    location ^~ /api/ {
$(nginx_proxy "        ")
$(nginx_headers "        " strict-origin-when-cross-origin)
    }

    # Magic-link landing (SPA route): the path carries the portal token - no access log, no Referer (s4.3)
    location ^~ /p/ {
        access_log off;
        try_files /index.html =404;
        add_header Cache-Control "no-cache" always;
$(nginx_headers "        " no-referrer)
    }

    # Public new-client form (SPA route): the path carries the form slug (s4.5)
    location ^~ /f/ {
        access_log off;
        try_files /index.html =404;
        add_header Cache-Control "no-cache" always;
        add_header X-Robots-Tag noindex always;
$(nginx_headers "        " no-referrer)
    }

    # Vite output: content-hashed file names, cache for a year
    location ^~ /assets/ {
        try_files \$uri =404;
        add_header Cache-Control "public, max-age=31536000, immutable" always;
$(nginx_headers "        " strict-origin-when-cross-origin)
    }

    # The SPA shell must always be revalidated so a deploy is picked up immediately
    location = /index.html {
        add_header Cache-Control "no-cache" always;
$(nginx_headers "        " strict-origin-when-cross-origin)
    }

    # SPA fallback
    location / {
        try_files \$uri /index.html;
    }
}
EOF
}

# Write the site, `nginx -t`, reload; restore the previous file when the test fails
apply_nginx_site() {
  local mode=$1 site; site="$(p "$NGINX_SITE")"
  local backup=""
  if [[ -f $site ]]; then backup=$(mktemp); cp -p "$site" "$backup"; fi
  render_nginx "$mode" | write_file "$NGINX_SITE" 644 root:root
  if (( DRY_RUN )); then printf '  [dry-run] nginx site (%s) -> %s\n' "$mode" "$site"; return 0; fi
  ln -sfn "$NGINX_SITE" /etc/nginx/sites-enabled/freelanceflow
  rm -f /etc/nginx/sites-enabled/default
  if ! nginx -t; then
    if [[ -n $backup ]]; then cp -p "$backup" "$site"; else rm -f "$site" /etc/nginx/sites-enabled/freelanceflow; fi
    rm -f "$backup"
    die "nginx -t failed - previous site config restored"
  fi
  rm -f "$backup"
  systemctl enable nginx >/dev/null
  if systemctl is-active --quiet nginx; then systemctl reload nginx; else systemctl start nginx; fi
  ok "nginx site ($mode) active"
}

setup_nginx_tls() {
  log "nginx + TLS"
  write_file /etc/letsencrypt/renewal-hooks/deploy/90-freelanceflow-reload-nginx.sh 755 root:root <<'HOOK'
#!/bin/sh
# certbot renewal: pick up the new certificate
systemctl reload nginx
HOOK
  if (( ! TLS )); then
    apply_nginx_site http
    warn "HTTP-only test mode (LETSENCRYPT_EMAIL empty) - see the summary"
    return 0
  fi
  if (( DRY_RUN )); then
    apply_nginx_site tls
    printf '  [dry-run] certbot certonly --webroot -w %s -d %s\n' "$ACME_ROOT" "$DOMAIN"
    return 0
  fi
  if [[ ! -s "$(cert_dir)/fullchain.pem" ]]; then
    # Bootstrap: serve the ACME challenge over HTTP, get the certificate, then switch to the TLS site
    apply_nginx_site http
    getent ahosts "$DOMAIN" >/dev/null || warn "$DOMAIN does not resolve yet - certbot will probably fail (check the DNS A record)"
    certbot certonly --webroot -w "$ACME_ROOT" -d "$DOMAIN" --email "$LE_EMAIL" --agree-tos --no-eff-email \
      --non-interactive --keep-until-expiring \
      || die "certbot failed. Check that the DNS A record of $DOMAIN points to this server and port 80 is reachable, then re-run setup. (Or empty LETSENCRYPT_EMAIL for HTTP-only test mode.)"
  fi
  apply_nginx_site tls
  systemctl enable --now certbot.timer >/dev/null 2>&1 || warn "certbot.timer not found - check automatic renewal (certbot renew --dry-run)"
  ok "certificate for $DOMAIN (auto-renewal by certbot.timer)"
}

# ------------------------------------------------------------------------------------- releases
health_ok() { curl -fsS --max-time 5 "http://127.0.0.1:$KESTREL_PORT/health" >/dev/null 2>&1; }

# Healthy within HEALTH_TIMEOUT; gives up early when systemd keeps restarting a crashing process
wait_health() {
  local i n0 n
  n0=$(systemctl show -p NRestarts --value "$SERVICE" 2>/dev/null || echo 0)
  for (( i = 0; i < HEALTH_TIMEOUT; i += 2 )); do
    health_ok && return 0
    n=$(systemctl show -p NRestarts --value "$SERVICE" 2>/dev/null || echo 0)
    if (( ${n:-0} - ${n0:-0} >= 3 )); then warn "$SERVICE crashed 3 times in a row"; return 1; fi
    sleep 2
  done
  return 1
}

current_release() { readlink -f "$CURRENT" 2>/dev/null || true; }

list_releases() {
  [[ -d $RELEASES_DIR ]] || return 0
  find "$RELEASES_DIR" -mindepth 1 -maxdepth 1 -type d -printf '%f\n' | sort
}

switch_current() {
  ln -sfn "$1" "$CURRENT.tmp"
  mv -Tf "$CURRENT.tmp" "$CURRENT"     # rename(2): atomic swap of the symlink
}

restart_and_check() {
  systemctl restart "$SERVICE"
  wait_health
}

show_failure_logs() {
  warn "Last API log lines:"
  journalctl -u "$SERVICE" -n 40 --no-pager -o cat >&2 || true
}

prune_releases() {
  local keep=$KEEP_RELEASES cur; cur=$(current_release)
  local all=() r
  mapfile -t all < <(list_releases)
  local n=${#all[@]} i
  for (( i = 0; i < n - keep; i++ )); do
    r="$RELEASES_DIR/${all[$i]}"
    if [[ $r != "$cur" ]]; then rm -rf -- "$r"; fi
  done
}

disk_free_mb() { df -Pm "$1" | awk 'NR==2 {print $4}'; }

validate_archive() {
  local archive=$1 listing
  [[ -f $archive ]] || die "Archive not found: $archive"
  listing=$(tar -tzf "$archive") || die "Not a readable .tar.gz: $archive"
  if grep -qE '(^/|(^|/)\.\.(/|$))' <<<"$listing"; then die "Archive contains absolute or .. paths - refusing"; fi
  grep -qxE '(\./)?app/FreelanceFlow\.Api\.dll' <<<"$listing" || die "Archive has no app/FreelanceFlow.Api.dll"
  grep -qxE '(\./)?web/index\.html' <<<"$listing" || die "Archive has no web/index.html"
}

do_deploy() {
  local archive=${1:-}
  [[ -n $archive ]] || die "Usage: deploy <archive.tar.gz>"
  archive=$(readlink -f "$archive")
  validate_archive "$archive"
  if (( DRY_RUN )); then
    printf '  [dry-run] would extract %s into %s/<timestamp>, swap %s, restart, wait for /health\n' "$archive" "$RELEASES_DIR" "$CURRENT"
    return 0
  fi
  [[ -f $ENV_FILE ]] || die "$ENV_FILE missing - run setup first"
  local free; free=$(disk_free_mb "$APP_DIR")
  (( free > 2048 )) || die "Only ${free} MB free on $APP_DIR - free some space first (old backups? sudo freelanceflow status)"

  local name dir prev
  name=$(date +%Y%m%d-%H%M%S)
  dir=$RELEASES_DIR/$name
  [[ -e $dir ]] && { name="$name-$$"; dir=$RELEASES_DIR/$name; }
  log "Deploying release $name"
  mkdir -p "$dir"
  tar -xzf "$archive" -C "$dir" --no-same-owner --no-same-permissions
  # never ship development settings (they may hold local secrets)
  rm -f "$dir"/app/appsettings.Development.json
  chown -R root:root "$dir"
  chmod -R u=rwX,go=rX "$dir"
  [[ -f $dir/manifest.json ]] && awk '{print "    " $0}' "$dir/manifest.json"

  prev=$(current_release)
  if [[ -n $prev && -d $prev ]]; then
    log "Pre-deploy database dump"
    backup_db pre-deploy || warn "pre-deploy dump failed - continuing"
  fi

  switch_current "$dir"
  log "Restarting $SERVICE and waiting for /health (up to ${HEALTH_TIMEOUT}s; first start runs the migrations)"
  if restart_and_check; then
    ok "release $name is healthy"
    strip_seed_from_env
    prune_releases
    return 0
  fi

  show_failure_logs
  if [[ -n $prev && -d $prev ]]; then
    warn "Health check failed - rolling back to $(basename "$prev")"
    switch_current "$prev"
    rm -rf -- "$dir"
    if restart_and_check; then
      warn "Rolled back; $(basename "$prev") is healthy again. If the failed release had already applied a migration, check the logs."
    else
      show_failure_logs
      die "Rollback release is unhealthy too - see: sudo freelanceflow logs"
    fi
    die "Deploy of $name failed (rolled back)"
  fi
  die "First release $name is not healthy and there is nothing to roll back to - see: sudo freelanceflow logs"
}

do_rollback() {
  (( DRY_RUN )) && { printf '  [dry-run] would switch %s to the previous release\n' "$CURRENT"; return 0; }
  local cur all=() i target=""
  cur=$(basename "$(current_release)")
  mapfile -t all < <(list_releases)
  for (( i = ${#all[@]} - 1; i >= 0; i-- )); do
    if [[ ${all[$i]} == "$cur" ]] && (( i > 0 )); then target=${all[$((i - 1))]}; break; fi
  done
  [[ -n $target ]] || die "No release older than $cur to roll back to (releases: ${all[*]:-none})"
  warn "Database migrations applied by $cur are NOT reverted. If the old code fails on the new schema, restore the pre-deploy dump (README-deploy.md)."
  log "Rolling back $cur -> $target"
  switch_current "$RELEASES_DIR/$target"
  if restart_and_check; then ok "now running $target"
  else show_failure_logs; die "$target is not healthy"; fi
}

# ------------------------------------------------------------------------------------- backup
backup_db() {
  local prefix=${1:-db} ts out
  ts=$(date +%Y%m%d-%H%M%S)
  out=$BACKUP_DIR/db/$prefix-$ts.dump
  install -d -m 0700 "$BACKUP_DIR" "$BACKUP_DIR/db"
  (umask 077; runuser -u postgres -- pg_dump -Fc -d "$DB_NAME" >"$out.tmp")
  mv -f "$out.tmp" "$out"
  ok "database -> $out ($(du -h "$out" | cut -f1))"
}

# Completed snapshot names (YYYYmmdd-HHMMSS), oldest first; *.partial = interrupted run, never listed
list_snapshots() {
  [[ -d $BACKUP_DIR/files ]] || return 0
  find "$BACKUP_DIR/files" -mindepth 1 -maxdepth 1 -type d -regextype posix-extended \
    -regex '.*/[0-9]{8}-[0-9]{6}(-[0-9]+)?' -printf '%f\n' | sort
}

# Hard-link snapshots: unchanged images cost no extra space, so 14 daily snapshots ~ 1x the image store
backup_files() {
  local ts snap prev=""
  ts=$(date +%Y%m%d-%H%M%S)
  install -d -m 0700 "$BACKUP_DIR/files"
  prev=$(list_snapshots | tail -n 1)
  snap=$BACKUP_DIR/files/$ts
  [[ -e $snap ]] && snap="$snap-$$"
  mkdir -p "$snap.partial"
  local opts=(-a --delete)
  [[ -n $prev ]] && opts+=("--link-dest=$BACKUP_DIR/files/$prev/files")
  rsync "${opts[@]}" "$FILES_DIR/" "$snap.partial/files/"
  rsync -a --delete "$KEYS_DIR/" "$snap.partial/keys/"
  mv "$snap.partial" "$snap"
  ok "files + keys -> $snap"
}

prune_backups() {
  local cutoff f name newest_dump newest_snap
  cutoff=$(date -d "-$BACKUP_RETENTION_DAYS days" +%Y%m%d-%H%M%S)
  newest_dump=$(find "$BACKUP_DIR/db" -maxdepth 1 -name 'db-*.dump' -printf '%f\n' | sort | tail -n 1)
  newest_snap=$(list_snapshots | tail -n 1)
  # db-<ts>.dump (daily) and pre-deploy-<ts>.dump (before each deploy); the newest daily dump is always kept
  while IFS= read -r f; do
    if [[ $f == "$newest_dump" || ! $f =~ ([0-9]{8}-[0-9]{6})\.dump$ ]]; then continue; fi
    if [[ ${BASH_REMATCH[1]} < $cutoff ]]; then rm -f -- "$BACKUP_DIR/db/$f"; fi
  done < <(find "$BACKUP_DIR/db" -maxdepth 1 -name '*.dump' -printf '%f\n')
  while IFS= read -r name; do
    if [[ $name == "$newest_snap" ]]; then continue; fi
    if [[ ${name:0:15} < $cutoff ]]; then rm -rf -- "${BACKUP_DIR:?}/files/$name"; fi
  done < <(list_snapshots)
  find "$BACKUP_DIR/files" -mindepth 1 -maxdepth 1 -type d -name '*.partial' -mmin +360 -exec rm -rf -- {} + 2>/dev/null || true
}

do_backup() {
  (( DRY_RUN )) && { printf '  [dry-run] would pg_dump %s and snapshot %s + %s into %s\n' "$DB_NAME" "$FILES_DIR" "$KEYS_DIR" "$BACKUP_DIR"; return 0; }
  log "Backup to $BACKUP_DIR"
  local free; free=$(disk_free_mb "$BACKUP_DIR")
  (( free > 5120 )) || warn "Only ${free} MB free on the backup disk"
  backup_db db
  backup_files
  prune_backups
  ok "kept $BACKUP_RETENTION_DAYS days; total $(du -sh "$BACKUP_DIR" | cut -f1)"
}

# ------------------------------------------------------------------------------------- status / logs
do_status() {
  (( DRY_RUN )) && { echo "  [dry-run] status needs a real server"; return 0; }
  local cur; cur=$(current_release)
  echo "${C_B}FreelanceFlow${C_0}  $PUBLIC_URL"
  echo "  service : $(systemctl is-active "$SERVICE" 2>/dev/null || true) ($(systemctl is-enabled "$SERVICE" 2>/dev/null || true))"
  echo "  health  : $(health_ok && echo "OK (127.0.0.1:$KESTREL_PORT/health)" || echo FAILING)"
  if [[ -n $cur ]]; then echo "  release : $(basename "$cur")"; else echo "  release : none"; fi
  [[ -n $cur && -f $cur/manifest.json ]] && awk '{print "            " $0}' "$cur/manifest.json"
  echo "  releases: $(list_releases | tr '\n' ' ')"
  echo "  nginx   : $(systemctl is-active nginx 2>/dev/null || true)   postgres: $(systemctl is-active "postgresql@${PG_VERSION}-main" 2>/dev/null || true)"
  if (( TLS )) && [[ -s "$(cert_dir)/fullchain.pem" ]]; then
    echo "  TLS     : expires $(openssl x509 -enddate -noout -in "$(cert_dir)/fullchain.pem" | cut -d= -f2)"
  elif (( ! TLS )); then
    echo "  TLS     : OFF (HTTP-only test mode)"
  fi
  echo "  backup  : last $(find "$BACKUP_DIR/db" -maxdepth 1 -name '*.dump' -printf '%f\n' 2>/dev/null | sort | tail -n 1)"
  echo "            next $(systemctl list-timers "$SERVICE-backup.timer" --no-legend 2>/dev/null | awk '{print $1, $2, $3}')"
  echo "  disk    :"; df -h / | sed 's/^/            /'
  local d
  for d in "$FILES_DIR" "$BACKUP_DIR" "$RELEASES_DIR" "/var/lib/postgresql"; do
    if [[ -d $d ]]; then printf '            %-8s %s\n' "$(du -sh "$d" 2>/dev/null | cut -f1)" "$d"; fi
  done
  return 0
}

do_logs() {
  if [[ ${1:-} == -f ]]; then journalctl -u "$SERVICE" -f -o cat
  else journalctl -u "$SERVICE" -n 200 --no-pager -o cat; fi
}

# ------------------------------------------------------------------------------------- setup
do_setup() {
  local archive=${1:-}
  [[ -z $archive ]] || validate_archive "$(readlink -f "$archive")"
  log "FreelanceFlow setup - Ubuntu $OS_VERSION, domain $DOMAIN, $( (( TLS )) && echo HTTPS || echo 'HTTP-only test mode')"
  if (( ! DRY_RUN )); then
    local total_gb quota_gb
    total_gb=$(df -Pk / | awk 'NR==2 {print int($2/1048576)}')
    quota_gb=$(( STORAGE_QUOTA_BYTES / 1073741824 ))
    (( quota_gb * 100 <= total_gb * 30 )) || warn "STORAGE_QUOTA_BYTES (${quota_gb} GB) is more than 30% of the ${total_gb} GB disk - images + backup snapshots may fill it"
  fi
  mkdir -p "$(p "$CONF_DIR")"
  install_cli
  save_server_config
  system_packages
  setup_timezone
  setup_swap
  setup_journald
  setup_firewall
  setup_fail2ban
  setup_unattended_upgrades
  setup_ssh_hardening
  install_dotnet
  install_postgres
  setup_user_dirs
  write_env_file
  setup_database
  write_systemd_units
  setup_nginx_tls

  if [[ -n $archive ]]; then
    do_deploy "$archive"
  elif (( ! DRY_RUN )) && [[ -e $CURRENT ]]; then
    log "Restarting $SERVICE with the new configuration"
    if restart_and_check; then ok "healthy"; strip_seed_from_env
    else show_failure_logs; die "$SERVICE is not healthy after setup"; fi
  else
    note "No release deployed yet: run  deploy-windows.ps1  (or: sudo freelanceflow deploy <archive.tar.gz>)"
  fi
  if (( ! DRY_RUN )) && [[ -f /var/run/reboot-required ]]; then
    note "Ubuntu updates need a reboot (kernel/libc): sudo reboot - API, nginx and PostgreSQL start again on their own"
  fi
  print_summary
}

print_summary() {
  echo
  echo "${C_B}================================ FreelanceFlow ================================${C_0}"
  echo "  URL            : $PUBLIC_URL"
  echo "  Designer login : $DESIGNER_USERNAME"
  if [[ -n $GENERATED_DESIGNER_PW ]]; then
    echo "  Password       : ${C_B}$GENERATED_DESIGNER_PW${C_0}   <- generated now, shown ONCE. Save it, then change it in the app."
  elif grep -q '^Seed__DesignerPassword=' "$(p "$ENV_FILE")" 2>/dev/null; then
    echo "  Password       : (from your config / earlier run; account not created yet - it is created on first start)"
  else
    echo "  Password       : (account already exists - unchanged)"
  fi
  (( GENERATED_DB_PW )) && echo "  DB password    : generated and stored only in $ENV_FILE"
  echo "  Config / env   : $SERVER_CONF  /  $ENV_FILE (600)"
  echo "  App / data     : $CURRENT -> releases, $FILES_DIR, $KEYS_DIR"
  echo "  Backups        : $BACKUP_DIR daily at $BACKUP_TIME, kept $BACKUP_RETENTION_DAYS days"
  echo "  Commands       : sudo freelanceflow status | logs [-f] | backup | rollback | deploy <archive>"
  if (( ! TLS )); then
    echo
    echo "  ${C_Y}HTTP-ONLY TEST MODE${C_0} - not for real use:"
    echo "   - passwords and cookies cross the network unencrypted; portal links point to https://$DOMAIN (not live yet)"
    echo "   - from http://$DOMAIN you can sign in and browse, but every write gets 403 (the CSRF cookie is Secure)"
    echo "   - full test: on your PC run  ssh -L 8080:127.0.0.1:80 ${SUDO_USER:-user}@<server>  and open $TUNNEL_ORIGIN"
    echo "   - go live: point the DNS A record at this server, set LETSENCRYPT_EMAIL, run setup again"
  fi
  local n
  for n in "${SUMMARY_NOTES[@]}"; do echo "  * $n"; done
  (( DRY_RUN )) && echo "  [dry-run] generated files are under: $ROOT"
  echo "${C_B}================================================================================${C_0}"
}

# ------------------------------------------------------------------------------------- main
usage() { sed -n '2,25p' "$SCRIPT_PATH" | sed 's/^# \{0,1\}//'; exit "${1:-0}"; }

main() {
  local cmd="" args=() dry_root=""
  while (( $# )); do
    case $1 in
      --config) CONFIG_ARG=${2:?--config needs a file}; shift 2 ;;
      --config=*) CONFIG_ARG=${1#*=}; shift ;;
      --dry-run) DRY_RUN=1; shift ;;
      --dry-run-root) dry_root=${2:?--dry-run-root needs a directory}; shift 2 ;;
      -h|--help) usage 0 ;;
      *) if [[ -z $cmd ]]; then cmd=$1; else args+=("$1"); fi; shift ;;
    esac
  done
  [[ -n $cmd ]] || usage 1
  if (( DRY_RUN )); then
    ROOT=${dry_root:-$(mktemp -d /tmp/freelanceflow-dry-run.XXXXXX)}
    mkdir -p "$ROOT"; ROOT=$(readlink -f "$ROOT")
    log "DRY RUN - nothing is installed; files go under $ROOT"
  fi

  case $cmd in
    render-nginx)
      load_config
      [[ ${args[0]:-} == tls || ${args[0]:-} == http ]] || die "Usage: render-nginx tls|http"
      render_nginx "${args[0]}" ;;
    setup)    require_root "$@"; check_os; load_config; do_setup "${args[0]:-}" ;;
    deploy)   require_root "$@"; load_config; (( DRY_RUN )) || install_cli; do_deploy "${args[0]:-}" ;;
    rollback) require_root "$@"; load_config; do_rollback ;;
    backup)   require_root "$@"; load_config; do_backup ;;
    status)   require_root "$@"; load_config; do_status ;;
    logs)     require_root "$@"; load_config; do_logs "${args[0]:-}" ;;
    *) die "Unknown command: $cmd (setup|deploy|rollback|status|backup|logs|render-nginx)" ;;
  esac
}

main "$@"
