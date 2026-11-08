import type { TaskListParams } from './types'

export const qk = {
  settings:  ['settings']  as const,
  banks:     ['banks']     as const,
  services:  ['services']  as const,
  dashboard: ['dashboard'] as const,
  search:    (q: string) => ['search', q] as const,

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

  tasks: {
    all:    ['tasks']                                             as const,
    lists:  ['tasks', 'list']                                     as const,
    list:   (params: TaskListParams) => ['tasks', 'list', params] as const,
    detail: (id: string)             => ['tasks', 'detail', id]   as const,
    notes:  (id: string)             => ['tasks', 'notes', id]    as const,
  },

  // Under 'projects' on purpose: every existing invalidateQueries(qk.projects.all) also refreshes the flow
  flow: {
    project:      (projectId: string) => ['projects', projectId, 'flow']          as const,
    startPreview: (projectId: string) => ['projects', projectId, 'start-preview'] as const,
    actions:      ['projects', 'flow-actions']                                    as const,
  },

  quotations: {
    all:    ['quotations']                                        as const,
    list:   (projectId: string) => ['quotations', 'list', projectId] as const,
    search: (params: object)    => ['quotations', 'search', params]  as const,
    detail: (id: string)        => ['quotations', id]                as const,
  },

  requests: {
    all:      ['requests']                                      as const,
    list:     (status: string) => ['requests', 'list', status]  as const,
    newCount: ['requests', 'new-count']                         as const,
    detail:   (id: string)     => ['requests', 'detail', id]    as const,
  },

  revisions: {
    all:      ['revisions']                                            as const,
    project:  (projectId: string) => ['revisions', projectId]   as const,
    suggest:  (deliverableId: string | null, types: string[]) => ['revisions', 'suggest', deliverableId, types] as const,
    progress: (projectId: string) => ['revisions', 'progress', projectId] as const,
  },

  portal: {
    me:       ['portal', 'me']                                         as const,
    projects: ['portal', 'projects']                                   as const,
    project:  (id: string) => ['portal', 'projects', id]               as const,
    timeline: (id: string) => ['portal', 'projects', id, 'timeline']   as const,
    quotations: (projectId?: string) => ['portal', 'quotations', projectId ?? 'all'] as const,
    quotation: (id: string) => ['portal', 'quotations', 'detail', id] as const,
    quotationVersion: (id: string, version: number) => ['portal', 'quotations', 'detail', id, 'version', version] as const,
    requests: (projectId?: string) => ['portal', 'requests', projectId ?? 'all'] as const,
    revisions: (id: string) => ['portal', 'projects', id, 'revisions'] as const,
  },
} as const
