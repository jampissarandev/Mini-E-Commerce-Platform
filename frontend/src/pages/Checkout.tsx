import { useCallback, useEffect, useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { AlertTriangle, Loader2, MapPin } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useCart } from '@/lib/useCart'
import { useCheckout } from '@/lib/useOrders'
import { usePaymentMode } from '@/lib/usePaymentMode'
import { useAuthStore } from '@/lib/auth-store'
import { useAddresses, useDefaultAddress } from '@/lib/useAddresses'
import { checkoutSchema, type CheckoutValues } from '@/lib/schemas/checkout'
import { formatCurrency } from '@/lib/utils'

const SHIPPING_FEE = 5.99

export function Checkout() {
  const [serverError, setServerError] = useState<string | null>(null)
  const [selectedAddressId, setSelectedAddressId] = useState<number | 'new'>('new')
  const { data: cartData, isLoading: cartLoading } = useCart()
  const { data: paymentModeData } = usePaymentMode()
  const { data: addressesData } = useAddresses()
  const checkout = useCheckout()
  const navigate = useNavigate()
  const fullName = useAuthStore((s) => s.customer?.fullName ?? '')

  const paymentMode = paymentModeData?.data
  const showPaymentWarning =
    paymentMode?.mode === 'AlwaysFail' ||
    paymentMode?.mode === 'FailIfAmountGreaterThan'

  const addresses = addressesData?.data ?? []
  const hasAddresses = addresses.length > 0

  const items = cartData?.data.items ?? []
  const subtotal = cartData?.data.total ?? 0
  const total = subtotal + SHIPPING_FEE

  // Redirect to cart if cart is empty (but not while still loading)
  useEffect(() => {
    if (!cartLoading && items.length === 0) {
      navigate('/cart', { replace: true })
    }
  }, [cartLoading, items.length, navigate])

  const form = useForm<CheckoutValues>({
    resolver: zodResolver(checkoutSchema),
    defaultValues: {
      fullName,
      street: '',
      city: '',
      postalCode: '',
      country: '',
      phone: '',
      saveAddress: false,
    },
  })

  const defaultAddress = useDefaultAddress()
  const addressesLoaded = !!addressesData?.data

  const handleAddressChange = useCallback(
    (value: string) => {
      if (value === 'new') {
        setSelectedAddressId('new')
        form.setValue('addressId', undefined)
        form.reset({
          fullName,
          street: '',
          city: '',
          postalCode: '',
          country: '',
          phone: '',
          saveAddress: false,
        })
      } else {
        const addrId = Number(value)
        setSelectedAddressId(addrId)
        const addr = addresses.find((a) => a.id === addrId)
        if (addr) {
          form.setValue('addressId', addrId)
          form.setValue('fullName', addr.fullName)
          form.setValue('street', addr.street)
          form.setValue('city', addr.city)
          form.setValue('postalCode', addr.postalCode)
          form.setValue('country', addr.country)
          form.setValue('phone', addr.phone)
        }
      }
    },
    [addresses, form, fullName],
  )

  // ADR 0004: pre-fill from the default address on first load.
  useEffect(() => {
    if (!addressesLoaded) return
    if (!defaultAddress) return
    if (selectedAddressId === defaultAddress.id) return
    handleAddressChange(String(defaultAddress.id))
  }, [addressesLoaded, defaultAddress, selectedAddressId, handleAddressChange])

  async function onSubmit(values: CheckoutValues) {
    setServerError(null)
    checkout.mutate(values, {
      onSuccess: (response) => {
        if (response.success && response.data) {
          navigate(`/orders/${response.data.id}`, { replace: true })
        } else {
          setServerError(response.error?.message ?? 'Checkout failed. Please try again.')
        }
      },
      onError: (error) => {
        if (
          error &&
          typeof error === 'object' &&
          'response' in error &&
          error.response &&
          typeof error.response === 'object' &&
          'data' in error.response
        ) {
          const data = error.response.data as { error?: { message?: string } }
          setServerError(data.error?.message ?? 'Checkout failed. Please try again.')
        } else {
          setServerError('An unexpected error occurred. Please try again.')
        }
      },
    })
  }

  if (cartLoading) {
    return (
      <div className="flex items-center justify-center py-12" role="status" aria-label="Loading checkout">
        <p className="text-muted-foreground">Loading checkout...</p>
      </div>
    )
  }

  if (items.length === 0) {
    return null // Redirect handled by useEffect
  }

  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-bold">Checkout</h1>

      {showPaymentWarning && paymentMode && (
        <div
          role="alert"
          className="flex items-start gap-3 rounded-md border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900"
          data-testid="payment-mode-banner"
        >
          <AlertTriangle className="h-5 w-5 shrink-0 text-amber-600" aria-hidden="true" />
          <div>
            <p className="font-medium">Demo: payment is configured to fail.</p>
            <p className="mt-1 text-amber-800">
              {paymentMode.mode === 'AlwaysFail'
                ? 'Every checkout will be declined by the mock payment service.'
                : `Checkouts over $${paymentMode.failIfAmountGreaterThan?.toFixed(2)} will be declined by the mock payment service.`}
            </p>
          </div>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-[1fr_380px]">
        {/* Shipping form */}
        <Card>
          <CardHeader>
            <CardTitle>Shipping Address</CardTitle>
          </CardHeader>
          <CardContent>
            <Form {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
                {hasAddresses && (
                  <div className="space-y-2">
                    <p className="text-sm font-medium">Choose a shipping address</p>
                    <div className="space-y-2">
                      {addresses.map((addr) => (
                        <label
                          key={addr.id}
                          className={`flex items-start gap-3 rounded-md border p-3 cursor-pointer transition-colors ${
                            selectedAddressId === addr.id
                              ? 'border-primary bg-primary/5'
                              : 'hover:bg-muted/50'
                          }`}
                        >
                          <input
                            type="radio"
                            name="addressPicker"
                            value={addr.id}
                            checked={selectedAddressId === addr.id}
                            onChange={() => handleAddressChange(String(addr.id))}
                            className="mt-0.5"
                          />
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2">
                              <span className="text-sm font-medium">{addr.fullName}</span>
                              {addr.isDefault && (
                                <span className="text-xs text-primary font-medium">(Default)</span>
                              )}
                            </div>
                            <p className="text-sm text-muted-foreground">{addr.street}</p>
                            <p className="text-sm text-muted-foreground">
                              {addr.city}, {addr.postalCode} · {addr.country}
                            </p>
                            <p className="text-sm text-muted-foreground">{addr.phone}</p>
                          </div>
                        </label>
                      ))}
                      <label
                        className={`flex items-start gap-3 rounded-md border p-3 cursor-pointer transition-colors ${
                          selectedAddressId === 'new'
                            ? 'border-primary bg-primary/5'
                            : 'hover:bg-muted/50'
                        }`}
                      >
                        <input
                          type="radio"
                          name="addressPicker"
                          value="new"
                          checked={selectedAddressId === 'new'}
                          onChange={() => handleAddressChange('new')}
                          className="mt-0.5"
                        />
                        <div className="flex items-center gap-2">
                          <MapPin className="h-4 w-4 text-muted-foreground" />
                          <span className="text-sm font-medium">Use a different address</span>
                        </div>
                      </label>
                    </div>
                  </div>
                )}
                <FormField
                  control={form.control}
                  name="fullName"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Full Name</FormLabel>
                      <FormControl>
                        <Input placeholder="Jane Doe" autoComplete="name" disabled={selectedAddressId !== 'new'} {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={form.control}
                  name="street"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Street</FormLabel>
                      <FormControl>
                        <Input
                          placeholder="123 Main St"
                          autoComplete="street-address"
                          disabled={selectedAddressId !== 'new'}
                          {...field}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <FormField
                    control={form.control}
                    name="city"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>City</FormLabel>
                        <FormControl>
                          <Input
                            placeholder="Springfield"
                            autoComplete="address-level2"
                            disabled={selectedAddressId !== 'new'}
                            {...field}
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <FormField
                    control={form.control}
                    name="postalCode"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Postal Code</FormLabel>
                        <FormControl>
                          <Input
                            placeholder="62701"
                            autoComplete="postal-code"
                            disabled={selectedAddressId !== 'new'}
                            {...field}
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <FormField
                    control={form.control}
                    name="country"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Country</FormLabel>
                        <FormControl>
                          <Input
                            placeholder="USA"
                            autoComplete="country-name"
                            disabled={selectedAddressId !== 'new'}
                            {...field}
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <FormField
                    control={form.control}
                    name="phone"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Phone</FormLabel>
                        <FormControl>
                          <Input
                            placeholder="+1-555-0100"
                            autoComplete="tel"
                            disabled={selectedAddressId !== 'new'}
                            {...field}
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>

                {serverError && (
                  <div className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">
                    {serverError}
                  </div>
                )}

                {selectedAddressId === 'new' && (
                  <label
                    className="flex items-center gap-2 rounded-md border border-input bg-background p-3 cursor-pointer hover:bg-muted/50"
                    data-testid="save-address-toggle"
                  >
                    <input
                      type="checkbox"
                      className="h-4 w-4 rounded border-input accent-primary"
                      checked={form.watch('saveAddress')}
                      onChange={(e) => form.setValue('saveAddress', e.target.checked)}
                    />
                    <span className="text-sm">Save this address for future checkouts</span>
                  </label>
                )}

                <Button
                  type="submit"
                  className="w-full"
                  size="lg"
                  disabled={checkout.isPending}
                >
                  {checkout.isPending ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Processing...
                    </>
                  ) : (
                    'Place Order'
                  )}
                </Button>
              </form>
            </Form>
          </CardContent>
        </Card>

        {/* Order summary */}
        <Card>
          <CardHeader>
            <CardTitle>Order Summary</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {items.map((item) => (
              <div key={item.id} className="flex gap-3">
                <div className="h-14 w-14 shrink-0 overflow-hidden rounded-md bg-muted">
                  {item.imageUrl ? (
                    <img
                      src={item.imageUrl}
                      alt={item.productName}
                      className="h-full w-full object-cover"
                    />
                  ) : (
                    <div className="flex h-full items-center justify-center text-xs text-muted-foreground">
                      No image
                    </div>
                  )}
                </div>
                <div className="flex flex-1 justify-between min-w-0">
                  <div className="min-w-0">
                    <p className="text-sm font-medium truncate">{item.productName}</p>
                    <p className="text-xs text-muted-foreground">Qty: {item.quantity}</p>
                  </div>
                  <span className="text-sm font-medium whitespace-nowrap">
                    {formatCurrency(item.subtotal)}
                  </span>
                </div>
              </div>
            ))}

            <div className="border-t pt-4 space-y-2">
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Subtotal</span>
                <span>{formatCurrency(subtotal)}</span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Shipping</span>
                <span>{formatCurrency(SHIPPING_FEE)}</span>
              </div>
              <div className="flex justify-between text-base font-semibold">
                <span>Total</span>
                <span>{formatCurrency(total)}</span>
              </div>
            </div>

            <Link to="/cart" className="block">
              <Button variant="outline" className="w-full">
                Back to Cart
              </Button>
            </Link>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
