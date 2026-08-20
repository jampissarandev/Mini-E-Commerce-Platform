import { useState } from 'react'
import { MapPin, Plus, Star, Trash2, Pencil, Loader2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import {
  useAddresses,
  useCreateAddress,
  useUpdateAddress,
  useDeleteAddress,
  useSetDefaultAddress,
} from '@/lib/useAddresses'
import { addressSchema, type AddressFormValues } from '@/lib/schemas/address'

export function Addresses() {
  const { data, isLoading } = useAddresses()
  const createAddress = useCreateAddress()
  const updateAddress = useUpdateAddress()
  const deleteAddress = useDeleteAddress()
  const setDefault = useSetDefaultAddress()

  const [dialogOpen, setDialogOpen] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [deletingId, setDeletingId] = useState<number | null>(null)

  const addresses = data?.data ?? []

  const form = useForm<AddressFormValues>({
    resolver: zodResolver(addressSchema),
    defaultValues: {
      fullName: '',
      street: '',
      city: '',
      postalCode: '',
      country: '',
      phone: '',
    },
  })

  function openCreateDialog() {
    setEditingId(null)
    form.reset({ fullName: '', street: '', city: '', postalCode: '', country: '', phone: '' })
    setDialogOpen(true)
  }

  function openEditDialog(addr: (typeof addresses)[0]) {
    setEditingId(addr.id)
    form.reset({
      fullName: addr.fullName,
      street: addr.street,
      city: addr.city,
      postalCode: addr.postalCode,
      country: addr.country,
      phone: addr.phone,
    })
    setDialogOpen(true)
  }

  function onSubmit(values: AddressFormValues) {
    if (editingId) {
      updateAddress.mutate(
        { id: editingId, request: values },
        { onSuccess: () => setDialogOpen(false) },
      )
    } else {
      createAddress.mutate(values, { onSuccess: () => setDialogOpen(false) })
    }
  }

  function handleDelete() {
    if (deletingId) {
      deleteAddress.mutate(deletingId, { onSuccess: () => setDeletingId(null) })
    }
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12" role="status" aria-label="Loading addresses">
        <p className="text-muted-foreground">Loading addresses...</p>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">My Addresses</h1>
        <Button onClick={openCreateDialog}>
          <Plus className="h-4 w-4 mr-2" />
          Add Address
        </Button>
      </div>

      {addresses.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <MapPin className="h-16 w-16 text-muted-foreground mb-4" />
          <p className="text-lg font-medium text-muted-foreground">No saved addresses</p>
          <p className="text-sm text-muted-foreground mt-1">
            Save an address to speed up checkout.
          </p>
          <Button onClick={openCreateDialog} className="mt-4">
            <Plus className="h-4 w-4 mr-2" />
            Add Address
          </Button>
        </div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2">
          {addresses.map((addr) => (
            <Card key={addr.id} className={addr.isDefault ? 'border-primary' : ''}>
              <CardHeader className="pb-3">
                <div className="flex items-center justify-between">
                  <CardTitle className="text-base">{addr.fullName}</CardTitle>
                  {addr.isDefault && (
                    <span className="inline-flex items-center gap-1 text-xs font-medium text-primary">
                      <Star className="h-3 w-3 fill-primary" />
                      Default
                    </span>
                  )}
                </div>
              </CardHeader>
              <CardContent className="space-y-2">
                <p className="text-sm">{addr.street}</p>
                <p className="text-sm">
                  {addr.city}, {addr.postalCode}
                </p>
                <p className="text-sm">{addr.country}</p>
                <p className="text-sm text-muted-foreground">{addr.phone}</p>
                <div className="flex gap-2 pt-2">
                  {!addr.isDefault && (
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setDefault.mutate(addr.id)}
                      disabled={setDefault.isPending}
                    >
                      Set as default
                    </Button>
                  )}
                  <Button variant="outline" size="sm" onClick={() => openEditDialog(addr)}>
                    <Pencil className="h-3 w-3 mr-1" />
                    Edit
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setDeletingId(addr.id)}
                  >
                    <Trash2 className="h-3 w-3 mr-1" />
                    Delete
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {/* Create / Edit dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editingId ? 'Edit Address' : 'Add Address'}</DialogTitle>
          </DialogHeader>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
              <FormField
                control={form.control}
                name="fullName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Full Name</FormLabel>
                    <FormControl>
                      <Input placeholder="Jane Doe" {...field} />
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
                      <Input placeholder="123 Main St" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <div className="grid grid-cols-2 gap-4">
                <FormField
                  control={form.control}
                  name="city"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>City</FormLabel>
                      <FormControl>
                        <Input placeholder="Springfield" {...field} />
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
                        <Input placeholder="62704" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <FormField
                  control={form.control}
                  name="country"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Country</FormLabel>
                      <FormControl>
                        <Input placeholder="US" {...field} />
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
                        <Input placeholder="+1-555-0100" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
              <DialogFooter>
                <Button type="submit" disabled={createAddress.isPending || updateAddress.isPending}>
                  {(createAddress.isPending || updateAddress.isPending) && (
                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  )}
                  {editingId ? 'Save Changes' : 'Add Address'}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* Delete confirmation dialog */}
      <Dialog open={deletingId !== null} onOpenChange={() => setDeletingId(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete Address</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-muted-foreground">
            Are you sure you want to delete this address? This action cannot be undone.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeletingId(null)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={handleDelete}
              disabled={deleteAddress.isPending}
            >
              {deleteAddress.isPending && (
                <Loader2 className="h-4 w-4 mr-2 animate-spin" />
              )}
              Delete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
