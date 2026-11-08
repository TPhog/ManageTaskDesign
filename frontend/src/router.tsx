import { createBrowserRouter, Navigate } from 'react-router-dom'
import AdminLayout from './features/admin/AdminLayout'
import PortalLayout from './features/portal/PortalLayout'
import PortalTokenPage from './features/portal/PortalTokenPage'
import PortalHomePage from './features/portal/PortalHomePage'
import PortalProjectPage from './features/portal/PortalProjectPage'
import LoginPage from './features/admin/auth/LoginPage'
import { ProtectedRoute } from './features/admin/ProtectedRoute'
import NotFoundPage from './features/admin/NotFoundPage'
import DashboardPage from './features/admin/dashboard/DashboardPage'
import SettingsPage from './features/admin/settings/SettingsPage'
import ServicesPage from './features/admin/services/ServicesPage'
import ProjectsPage from './features/admin/projects/ProjectsPage'
import ProjectDetailPage from './features/admin/projects/detail/ProjectDetailPage'
import ClientsPage from './features/admin/clients/ClientsPage'
import QuotationPage from './features/admin/quotations/QuotationPage'
import QuotationsPage from './features/admin/quotations/QuotationsPage'
import TasksPage from './features/admin/tasks/TasksPage'
import RequestsPage from './features/admin/requests/RequestsPage'

export const router = createBrowserRouter([
  {
    path: '/admin/login',
    element: <LoginPage />,
  },
  {
    path: '/admin',
    element: <ProtectedRoute><AdminLayout /></ProtectedRoute>,
    errorElement: <NotFoundPage />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'requests', element: <RequestsPage /> },
      { path: 'requests/:id', element: <RequestsPage /> },
      { path: 'settings', element: <SettingsPage /> },
      { path: 'services', element: <ServicesPage /> },
      { path: 'projects', element: <ProjectsPage /> },
      { path: 'projects/:id', element: <ProjectDetailPage /> },
      { path: 'tasks', element: <TasksPage /> },
      { path: 'clients', element: <ClientsPage /> },
      { path: 'quotations', element: <QuotationsPage /> },
      { path: 'quotations/:id', element: <QuotationPage /> },
    ],
  },
  {
    path: '/',
    element: <Navigate to="/admin" replace />,
  },
  {
    path: '/p',
    element: <PortalTokenPage />,
  },
  {
    path: '/p/:token',
    element: <PortalTokenPage />,
  },
  {
    path: '/portal',
    element: <PortalLayout />,
    children: [
      { index: true, element: <PortalHomePage /> },
      { path: 'projects/:id', element: <PortalProjectPage /> },
    ],
  },
  {
    path: '*',
    element: <NotFoundPage />,
  },
])
