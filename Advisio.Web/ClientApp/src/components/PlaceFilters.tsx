import { Input } from "@/components/ui/input"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Search } from "lucide-react"

interface PlaceFiltersProps {
  searchQuery: string
  onSearchChange: (query: string) => void
  selectedCategory: string
  onCategoryChange: (category: string) => void
  selectedType: string
  onTypeChange: (type: string) => void
  sortBy: string
  onSortChange: (sort: string) => void
  categories: string[]
}

const PLACE_TYPES = [
  "Все типы",
  "Концерт",
  "Ресторан",
  "Парк",
  "Турнир",
  "Выставка",
  "Фестиваль",
  "Спектакль",
  "Кино",
  "Образование",
  "Эко тропа",
]

const SORT_OPTIONS = [
  { value: "default", label: "По умолчанию" },
  { value: "name-asc", label: "По названию (А-Я)" },
  { value: "name-desc", label: "По названию (Я-А)" },
  { value: "likes-desc", label: "По лайкам (больше)" },
  { value: "likes-asc", label: "По лайкам (меньше)" },
  { value: "date-desc", label: "По дате (новые)" },
  { value: "date-asc", label: "По дате (старые)" },
]

const PlaceFilters = ({
  searchQuery,
  onSearchChange,
  selectedCategory,
  onCategoryChange,
  selectedType,
  onTypeChange,
  sortBy,
  onSortChange,
  categories,
}: PlaceFiltersProps) => {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <Input
          placeholder="Поиск мест..."
          value={searchQuery}
          onChange={(e) => onSearchChange(e.target.value)}
          className="pl-10 bg-card border-border focus:border-primary"
        />
      </div>

      <Select value={selectedCategory} onValueChange={onCategoryChange}>
        <SelectTrigger className="bg-card border-border">
          <SelectValue placeholder="Все категории" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">Все категории</SelectItem>
          {categories.map((cat) => (
            <SelectItem key={cat} value={cat}>
              {cat}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <Select value={selectedType} onValueChange={onTypeChange}>
        <SelectTrigger className="bg-card border-border">
          <SelectValue placeholder="Все типы" />
        </SelectTrigger>
        <SelectContent>
          {PLACE_TYPES.map((type) => (
            <SelectItem key={type} value={type === "Все типы" ? "all" : type}>
              {type}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <Select value={sortBy} onValueChange={onSortChange}>
        <SelectTrigger className="bg-card border-border">
          <SelectValue placeholder="Сортировка" />
        </SelectTrigger>
        <SelectContent>
          {SORT_OPTIONS.map((option) => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  )
}

export default PlaceFilters





