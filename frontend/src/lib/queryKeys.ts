import type { TaskListParams } from './types'

export const qk = {
  settings:  ['settings']  as const,
  banks:     ['banks']     as const,
  services:  ['services']  as const,
  dashboard: ['dashboard'] as const,
  dashboardBlocks: {
    schedule:  ['dashboard', 'schedule']                              as const,
    health:    ['dashboard', 'project-health']                        as const,
    money:     (period: string) => ['dashboard', 'money', period]     as const,
    activity:  ['dashboard', 'client-activity']                       as const,
    setup:     ['dashboard', 'setup']                                 as const,
  },
  digest:    ['settings', 'digest'] as const,
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
    calendarAll: ['tasks', 'calendar']                            as const,
    calendar: (from: string, to: string, projectId?: string) => ['tasks', 'calendar', from, to, projectId ?? ''] as const,
  },

  taskViews: ['task-views'] as const,

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

  intake: {
    all:      ['intake']                                                          as const,
    list:     (status: string, page: number) => ['intake', 'list', status, page]  as const,
    newCount: ['intake', 'new-count']                                             as const,
    detail:   (id: string) => ['intake', 'detail', id]                            as const,
  },

  revisions: {
    all:      ['revisions']                                            as const,
    project:  (projectId: string) => ['revisions', projectId]   as const,
    suggest:  (deliverableId: string | null, types: string[]) => ['revisions', 'suggest', deliverableId, types] as const,
    progress: (projectId: string) => ['revisions', 'progress', projectId] as const,
    quotePreview: (projectId: string, itemIds: string[]) => ['revisions', 'quote-preview', projectId, itemIds] as const,
  },

  images: {
    all:     ['images']                                                                 as const,
    task:    (taskId: string) => ['images', 'task', taskId]                             as const,
    project: (projectId: string, ownerType?: string) => ['images', 'project', projectId, ownerType ?? 'all'] as const,
  },

  storage: {
    all:    ['storage']                                       as const,
    usage:  ['storage', 'usage']                              as const,
    images: (params: object) => ['storage', 'images', params] as const,
    projects: ['storage', 'projects']                         as const,
    intake:   ['storage', 'intake']                           as const,
    originalCleanup: (months: number) => ['storage', 'original-cleanup', months] as const,
  },

  watermark: ['settings', 'watermark'] as const,

  requestTemplates: {
    all:    ['request-templates']                      as const,
    detail: (id: string) => ['request-templates', id]  as const,
  },
  intakeLink: ['settings', 'intake'] as const,
  publicIntake: (slug: string) => ['public-intake', slug] as const,

  portal: {
    images:   (id: string) => ['portal', 'projects', id, 'images']    as const,
    me:       ['portal', 'me']                                         as const,
    studio:   ['portal', 'studio']                                     as const,
    projects: ['portal', 'projects']                                   as const,
    project:  (id: string) => ['portal', 'projects', id]               as const,
    timeline: (id: string) => ['portal', 'projects', id, 'timeline']   as const,
    quotations: (projectId?: string) => ['portal', 'quotations', projectId ?? 'all'] as const,
    quotation: (id: string) => ['portal', 'quotations', 'detail', id] as const,
    quotationVersion: (id: string, version: number) => ['portal', 'quotations', 'detail', id, 'version', version] as const,
    requests: (projectId?: string) => ['portal', 'requests', projectId ?? 'all'] as const,
    revisions: (id: string) => ['portal', 'projects', id, 'revisions'] as const,
    requestTemplates: ['portal', 'request-templates'] as const,
  },
} as const
