/** "Studio Mây" → head "Studio ", tail "Mây" (the masthead sets the last word in italic oxblood). One word → no tail. */
export function splitWordmark(name: string): { head: string; tail: string } {
  const words = name.trim().split(/\s+/).filter(Boolean)
  if (words.length < 2) return { head: words.join(''), tail: '' }
  return { head: `${words.slice(0, -1).join(' ')} `, tail: words[words.length - 1] }
}
