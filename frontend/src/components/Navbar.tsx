import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { CartBadge } from '@/components/CartBadge'
import { useAuthStore } from '@/lib/auth-store'

export function Navbar() {
  const { user, isAuthenticated, isAdmin, logout } = useAuthStore()

  return (
    <header className="sticky top-0 z-50 w-full border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
      <div className="container flex h-14 items-center">
        <Link to="/" className="mr-6 flex items-center space-x-2">
          <span className="font-bold text-xl">Mini E-Commerce</span>
        </Link>
        <nav className="flex items-center space-x-4 flex-1">
          <Link
            to="/products"
            className="text-sm font-medium text-muted-foreground hover:text-foreground transition-colors"
          >
            Products
          </Link>
          {isAuthenticated() && (
            <Link
              to="/orders"
              className="text-sm font-medium text-muted-foreground hover:text-foreground transition-colors"
            >
              Orders
            </Link>
          )}
          {isAuthenticated() && isAdmin() && (
            <Link
              to="/admin"
              className="text-sm font-medium text-muted-foreground hover:text-foreground transition-colors"
            >
              Admin
            </Link>
          )}
        </nav>
        <div className="flex items-center space-x-2">
          <CartBadge />
          {isAuthenticated() ? (
            <>
              <span className="text-sm text-muted-foreground hidden sm:inline">
                Welcome, {user?.fullName}
              </span>
              <Button variant="outline" size="sm" onClick={logout}>
                Log out
              </Button>
            </>
          ) : (
            <>
              <Link to="/login">
                <Button variant="outline" size="sm">
                  Log in
                </Button>
              </Link>
              <Link to="/register">
                <Button size="sm">
                  Register
                </Button>
              </Link>
            </>
          )}
        </div>
      </div>
    </header>
  )
}
