import { Button } from '@/components/ui/button'
import type { CategoryDto } from '@/lib/types'

interface CategoryFilterProps {
  categories: CategoryDto[]
  selectedCategory?: string
  onCategoryChange: (slug: string | undefined) => void
}

export function CategoryFilter({ categories, selectedCategory, onCategoryChange }: CategoryFilterProps) {
  return (
    <div className="flex flex-wrap gap-2">
      <Button
        variant={selectedCategory === undefined ? 'default' : 'outline'}
        size="sm"
        onClick={() => onCategoryChange(undefined)}
        aria-pressed={selectedCategory === undefined}
      >
        All
      </Button>
      {categories.map((category) => (
        <Button
          key={category.slug}
          variant={selectedCategory === category.slug ? 'default' : 'outline'}
          size="sm"
          onClick={() => onCategoryChange(category.slug)}
          aria-pressed={selectedCategory === category.slug}
        >
          {category.name}
          <span className="ml-1 text-xs text-muted-foreground">({category.productCount})</span>
        </Button>
      ))}
    </div>
  )
}
