import { useCallback, useState } from 'react'
import { Button } from '@/components/ui/button'
import { Upload, X } from 'lucide-react'
import type { ProductImageDto } from '@/lib/types'

interface ImageUploadProps {
  productId: number
  images: ProductImageDto[]
  onUpload: (files: File[]) => void
  onDeleteImage: (imageId: number) => void
  isUploading: boolean
}

export function ImageUpload({
  images,
  onUpload,
  onDeleteImage,
  isUploading,
}: ImageUploadProps) {
  const [dragOver, setDragOver] = useState(false)

  const handleFileChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const files = Array.from(e.target.files || [])
      if (files.length > 0) {
        onUpload(files)
      }
      // Reset the input so the same file can be selected again
      e.target.value = ''
    },
    [onUpload],
  )

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault()
      setDragOver(false)
      const files = Array.from(e.dataTransfer.files).filter((f) =>
        f.type.startsWith('image/'),
      )
      if (files.length > 0) {
        onUpload(files)
      }
    },
    [onUpload],
  )

  return (
    <div className="space-y-4">
      {/* Existing images */}
      {images.length > 0 && (
        <div className="grid grid-cols-3 gap-3">
          {images.map((image) => (
            <div key={image.id} className="group relative">
              <img
                src={image.url}
                alt={`Product image ${image.sortOrder + 1}`}
                className="h-24 w-full rounded-md border object-cover"
              />
              <button
                type="button"
                onClick={() => onDeleteImage(image.id)}
                className="absolute -top-2 -right-2 hidden h-6 w-6 items-center justify-center rounded-full bg-destructive text-xs text-white group-hover:flex"
                aria-label={`Delete image ${image.sortOrder + 1}`}
              >
                <X className="h-3 w-3" />
              </button>
            </div>
          ))}
        </div>
      )}

      {/* Upload zone */}
      <div
        onDragOver={(e) => {
          e.preventDefault()
          setDragOver(true)
        }}
        onDragLeave={() => setDragOver(false)}
        onDrop={handleDrop}
        className={`flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-6 transition-colors ${
          dragOver
            ? 'border-primary bg-primary/5'
            : 'border-muted-foreground/25 hover:border-muted-foreground/50'
        }`}
      >
        <Upload className="mb-2 h-8 w-8 text-muted-foreground" />
        <p className="mb-2 text-sm text-muted-foreground">
          Drag and drop images here, or
        </p>
        <label htmlFor="image-upload">
          <Button variant="outline" size="sm" asChild>
            <span>
              {isUploading ? 'Uploading...' : 'Choose files'}
            </span>
          </Button>
          <input
            id="image-upload"
            type="file"
            accept="image/*"
            multiple
            className="hidden"
            onChange={handleFileChange}
            disabled={isUploading}
          />
        </label>
        <p className="mt-1 text-xs text-muted-foreground">
          PNG, JPG, WebP up to 5MB each
        </p>
      </div>
    </div>
  )
}
