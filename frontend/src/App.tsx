import { Routes, Route } from 'react-router-dom'
import { Layout } from '@/components/Layout'
import { AdminLayout } from '@/components/AdminLayout'
import { ProtectedRoute } from '@/components/ProtectedRoute'
import { Home } from '@/pages/Home'
import { Products } from '@/pages/Products'
import { Cart } from '@/pages/Cart'
import { Checkout } from '@/pages/Checkout'
import { Login } from '@/pages/Login'
import { Register } from '@/pages/Register'
import { NotFound } from '@/pages/NotFound'
import { AdminDashboard } from '@/pages/admin/Dashboard'
import { AdminProducts } from '@/pages/admin/Products'
import { AdminOrders } from '@/pages/admin/Orders'

function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<Home />} />
        <Route path="/products" element={<Products />} />
        <Route path="/cart" element={<Cart />} />
        <Route path="/checkout" element={<Checkout />} />
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
      </Route>

      <Route
        path="/admin"
        element={
          <ProtectedRoute requiredRole="Admin">
            <AdminLayout />
          </ProtectedRoute>
        }
      >
        <Route index element={<AdminDashboard />} />
        <Route path="products" element={<AdminProducts />} />
        <Route path="orders" element={<AdminOrders />} />
      </Route>

      <Route path="*" element={<NotFound />} />
    </Routes>
  )
}

export default App
