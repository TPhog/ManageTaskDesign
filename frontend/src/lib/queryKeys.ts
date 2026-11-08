export const qk = {
  settings:  ['settings']  as const,
  banks:     ['banks']     as const,
  services:  ['services']  as const,

  clients: {
    all:    ['clients']                           as const,
    list:   (q?: string)   => ['clients', 'list', q]   as const,
    detail: (id: string)   => ['clients', id]          as const,
  },

  projects: {
    all:    ['projects']                               as const,
    list:   (clientId?: string) => ['projects', 'list', clientId] as const,
    detail: (id: string)        => ['projects', id]               as const,
  },

  quotations: {
    list:   (projectId: string) => ['quotations', 'list', projectId] as const,
    detail: (id: string)        => ['quotations', id]                as const,
  },

  portal: {
    me:       ['portal', 'me']                                         as const,
    projects: ['portal', 'projects']                                   as const,
    project:  (id: string) => ['portal', 'projects', id]               as const,
    timeline: (id: string) => ['portal', 'projects', id, 'timeline']   as const,
  },
} as const
