import { Card, CardContent, CardFooter } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Heart, Star, MapPin, ExternalLink } from "lucide-react"
import { Link } from "react-router-dom"
import { cn } from "@/lib/utils"

export interface Place {
  id: number;
  name: string;
  description: string;
  type?: string;
  address?: string;
  imageUrl?: string;
  latitude?: number;
  longitude?: number;
  likes: number;
  categories: string[];
  isLiked?: boolean;
  isFavorite?: boolean;
  averageRating?: number;
  reviewsCount?: number;
  isSubscribed?: boolean;
  isVisited?: boolean;
}

interface PlaceCardProps {
  place: Place
  onLike?: (id: number) => void
  onFavorite?: (id: number) => void
  isAdmin?: boolean
  onEdit?: (id: number) => void
  className?: string
}

const PlaceCard = ({
  place,
  onLike,
  onFavorite,
  isAdmin = false,
  onEdit,
  className 
}: PlaceCardProps) => {
  const truncatedDescription = place.description.length > 150 
    ? place.description.substring(0, 150) + "..." 
    : place.description

  return (
    <Card variant="place" className={cn("flex flex-col h-full", className)}>
      {/* Image */}
      {place.imageUrl ? (
        <div className="relative h-48 overflow-hidden">
          <img
            src={place.imageUrl}
            alt={place.name}
            className="w-full h-full object-cover transition-transform duration-500 hover:scale-105"
          />
          <div className="absolute inset-0 bg-gradient-to-t from-foreground/30 to-transparent" />
        </div>
      ) : (
        <div className="h-48 bg-muted flex items-center justify-center">
          <MapPin className="h-12 w-12 text-muted-foreground/50" />
        </div>
      )}

      <CardContent className="flex-1 pt-5">
        {/* Title */}
        <h3 className="font-display text-xl font-semibold mb-3 text-foreground line-clamp-2">
          {place.name}
        </h3>

        {/* Badges */}
        <div className="flex flex-wrap gap-2 mb-3">
          {place.type && <Badge variant="type">{place.type}</Badge>}
          {place.categories.slice(0, 2).map((cat) => (
            <Badge key={cat} variant="category">{cat}</Badge>
          ))}
          {place.categories.length > 2 && (
            <Badge variant="muted">+{place.categories.length - 2}</Badge>
          )}
        </div>

        {/* Description */}
        <p className="text-sm text-muted-foreground leading-relaxed whitespace-pre-line">
          {truncatedDescription}
        </p>
      </CardContent>

      <CardFooter className="flex items-center justify-between pt-4 border-t border-border/50">
        {/* Likes count */}
        <span className="text-sm text-muted-foreground flex items-center gap-1">
          <Heart className="h-4 w-4" />
          {place.likes}
        </span>

        {/* Actions */}
        <div className="flex items-center gap-2">
          <Button
            variant={place.isFavorite ? "favorite" : "favorite-outline"}
            size="icon"
            onClick={() => onFavorite?.(place.id)}
            className="h-9 w-9"
          >
            <Star className={cn("h-4 w-4", place.isFavorite && "fill-current")} />
          </Button>
          <Button
            variant={place.isLiked ? "like" : "like-outline"}
            size="icon"
            onClick={() => onLike?.(place.id)}
            className="h-9 w-9"
          >
            <Heart className={cn("h-4 w-4", place.isLiked && "fill-current")} />
          </Button>
        </div>
      </CardFooter>

      {/* View Details Link */}
      <div className="px-6 pb-5 flex gap-2">
        <Link to={`/place/${place.id}`} className="flex-1">
          <Button variant="default" className="w-full gap-2" size="sm">
            Подробнее
            <ExternalLink className="h-3.5 w-3.5" />
          </Button>
        </Link>
        {isAdmin && (
          <Button
            variant="warning"
            size="sm"
            onClick={() => onEdit?.(place.id)}
          >
            Редактировать
          </Button>
        )}
      </div>
    </Card>
  )
}

export default PlaceCard


