import { createBrowserRouter, Navigate } from 'react-router-dom'
import AdminLayout from './features/admin/AdminLayout'
import PortalLayout from './features/portal/PortalLayout'
import LoginPage from './features/admin/auth/LoginPage'
import { ProtectedRoute } from './features/admin/ProtectedRoute'
import SettingsPage from './features/admin/settings/SettingsPage'
import ServicesPage from './features/admin/services/ServicesPage'
import ProjectsPage from './features/admin/projects/ProjectsPage'
import ProjectDetailPage from './features/admin/projects/detail/ProjectDetailPage'
import ClientsPage from './features/admin/clients/ClientsPage'

export const router = createBrowserRouter([
  {
    path: '/admin/login',
    element: <LoginPage />,
  },
  {
    path: '/admin',
    element: <ProtectedRoute><AdminLayout /></ProtectedRoute>,
    children: [
      { index: true, element: <div className="text-gray-500 text-sm">Tổng quan — đang phát triển</div> },
      { path: 'settings', element: <SettingsPage /> },
      { path: 'services', element: <ServicesPage /> },
      { path: 'projects', element: <ProjectsPage /> },
      { path: 'projects/:id', element: <ProjectDetailPage /> },
      { path: 'clients', element: <ClientsPage /> },
    ],
  },
  {
    path: '/',
    element: <Navigate to="/admin" replace />,
  },
  {
    path: '/p',
    element: <PortalLayout />,
    children: [],
  },
])
