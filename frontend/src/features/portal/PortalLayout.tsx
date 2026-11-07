import { Outlet } from 'react-router-dom'

export default function PortalLayout() {
  return (
    <div className="min-h-screen bg-white">
      <header className="h-14 border-b border-gray-100 flex items-center px-4">
        <span className="font-medium text-gray-700">Portal Khách Hàng</span>
      </header>
      <main className="max-w-2xl mx-auto p-4">
        <Outlet />
      </main>
    </div>
  )
}
