import { Navigate } from 'react-router-dom'
import { useAuthStore } from '@/lib/auth-store'

interface ProtectedRouteProps {
  children: React.ReactNode
  requiredRole?: string
}

export function ProtectedRoute({ children, requiredRole }: ProtectedRouteProps) {
  const { isAuthenticated, isAdmin } = useAuthStore()

  if (!isAuthenticated()) {
    return <Navigate to="/login" replace />
  }

  if (requiredRole === 'Admin' && !isAdmin()) {
    return <Navigate to="/login" replace />
  }

  return <>{children}</>
}
