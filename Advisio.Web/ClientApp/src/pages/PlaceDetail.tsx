import { useState, useEffect } from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import Layout from "@/components/Layout";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { 
  Heart, 
  Star, 
  MapPin, 
  Map, 
  ChevronRight,
  ExternalLink,
  Edit,
  Trash2
} from "lucide-react";
import { cn, isAdmin } from "@/lib/utils";
import { toast } from "sonner";
import { useAuth } from "@/contexts/AuthContext";
import EditPlaceDialog from "@/components/EditPlaceDialog";
import type { Place } from "@/components/PlaceCard";

const PlaceDetail = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  const [place, setPlace] = useState<Place | null>(null);
  const [loading, setLoading] = useState(true);
  const [editDialogOpen, setEditDialogOpen] = useState(false);
  const [categories, setCategories] = useState<Array<{ id: number; name: string }>>([]);

  useEffect(() => {
    if (id) {
      loadPlace(parseInt(id));
    }
    loadCategories();
  }, [id]);

  const loadCategories = async () => {
    try {
      const response = await fetch("/api/Categories");
      if (response.ok) {
        const data = await response.json();
        setCategories(data.map((cat: { id: number; name: string }) => ({ id: cat.id, name: cat.name })));
      }
    } catch (error) {
      console.error("Failed to load categories:", error);
    }
  };

  const loadPlace = async (placeId: number) => {
    setLoading(true);
    try {
      const userId = user?.id;
      const url = userId 
        ? `/api/Places/${placeId}?userId=${userId}`
        : `/api/Places/${placeId}`;
      const response = await fetch(url);
      if (response.ok) {
        const data = await response.json();
        setPlace({
          id: data.id,
          name: data.name || "Без названия",
          description: data.description || "",
          type: data.type || null,
          address: data.address || null,
          imageUrl: data.imageUrl || null,
          latitude: data.latitude ? Number(data.latitude) : null,
          longitude: data.longitude ? Number(data.longitude) : null,
          likes: data.likesCount || 0,
          categories: data.categories || [],
          isLiked: data.isLiked || false,
          isFavorite: data.isFavorite || false,
        });
      } else if (response.status === 404) {
        toast.error("Место не найдено");
        navigate("/places");
      } else {
        toast.error("Не удалось загрузить место");
      }
    } catch (error) {
      console.error("Failed to load place:", error);
      toast.error("Ошибка при загрузке места");
    } finally {
      setLoading(false);
    }
  };

  const handleLike = async () => {
    if (!place || !user) {
      toast.error("Необходимо войти в систему");
      return;
    }
    try {
      const isLiked = place.isLiked;

      const response = await fetch(`/api/Places/${place.id}/likes?userId=${user.id}`, {
        method: isLiked ? "DELETE" : "POST",
      });

      if (response.ok) {
        setPlace((prev) =>
          prev
            ? {
                ...prev,
                isLiked: !prev.isLiked,
                likes: prev.isLiked ? prev.likes - 1 : prev.likes + 1,
              }
            : null
        );
        toast.success(isLiked ? "Лайк удален" : "Место лайкнуто");
      } else {
        toast.error("Не удалось обновить лайк");
      }
    } catch (error) {
      console.error("Failed to like place:", error);
      toast.error("Ошибка при обновлении лайка");
    }
  };

  const handleFavorite = async () => {
    if (!place || !user) {
      toast.error("Необходимо войти в систему");
      return;
    }
    try {
      const isFavorite = place.isFavorite;

      const response = await fetch(`/api/Places/${place.id}/favorites?userId=${user.id}`, {
        method: isFavorite ? "DELETE" : "POST",
      });

      if (response.ok) {
        setPlace((prev) =>
          prev ? { ...prev, isFavorite: !prev.isFavorite } : null
        );
        toast.success(isFavorite ? "Удалено из избранного" : "Добавлено в избранное");
      } else {
        toast.error("Не удалось обновить избранное");
      }
    } catch (error) {
      console.error("Failed to favorite place:", error);
      toast.error("Ошибка при обновлении избранного");
    }
  };

  const openMap = () => {
    if (place?.latitude && place?.longitude) {
      window.open(
        `https://www.google.com/maps?q=${place.latitude},${place.longitude}`,
        "_blank"
      );
    }
  };

  const handleDelete = async () => {
    if (!place || !user || !isAdmin(user.role)) {
      return;
    }
    
    if (!confirm(`Вы уверены, что хотите удалить место "${place.name}"?`)) {
      return;
    }

    try {
      const response = await fetch(`/api/Places/${place.id}`, {
        method: "DELETE",
      });

      if (response.ok) {
        toast.success("Место удалено");
        navigate("/places");
      } else {
        const error = await response.json();
        toast.error(error.error || "Не удалось удалить место");
      }
    } catch (error) {
      console.error("Failed to delete place:", error);
      toast.error("Ошибка при удалении места");
    }
  };

  const handleEdit = () => {
    setEditDialogOpen(true);
  };

  const handlePlaceUpdated = () => {
    if (id) {
      loadPlace(parseInt(id));
    }
  };

  if (loading) {
    return (
      <Layout>
        <div className="container py-16 text-center">
          <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-muted flex items-center justify-center animate-pulse">
            <MapPin className="h-8 w-8 text-muted-foreground" />
          </div>
          <p className="text-muted-foreground">Загрузка...</p>
        </div>
      </Layout>
    );
  }

  if (!place) {
    return (
      <Layout>
        <div className="container py-16 text-center">
          <h1 className="text-2xl font-display font-bold mb-4">
            Место не найдено
          </h1>
          <Link to="/places">
            <Button>Вернуться к списку</Button>
          </Link>
        </div>
      </Layout>
    );
  }

  return (
    <Layout>
      <div className="container py-8 md:py-12">
        {/* Breadcrumbs */}
        <nav className="flex items-center gap-2 text-sm text-muted-foreground mb-6">
          <Link to="/places" className="hover:text-foreground transition-colors">
            Места отдыха
          </Link>
          <ChevronRight className="h-4 w-4" />
          <span className="text-foreground font-medium">{place.name}</span>
        </nav>

        <Card variant="elevated" className="overflow-hidden animate-fade-in">
          {/* Image */}
          {place.imageUrl ? (
            <div className="relative h-64 md:h-96 overflow-hidden">
              <img
                src={place.imageUrl}
                alt={place.name}
                className="w-full h-full object-cover"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-foreground/50 to-transparent" />
            </div>
          ) : (
            <div className="h-64 md:h-80 bg-muted flex items-center justify-center">
              <MapPin className="h-16 w-16 text-muted-foreground/30" />
            </div>
          )}

          <CardContent className="p-6 md:p-10">
            {/* Title */}
            <h1 className="text-3xl md:text-4xl font-display font-bold text-foreground mb-4">
              {place.name}
            </h1>

            {/* Badges */}
            <div className="flex flex-wrap gap-2 mb-6">
              {place.type && (
                <Badge variant="type" className="text-sm px-3 py-1">
                  {place.type}
                </Badge>
              )}
              {place.categories.map((cat) => (
                <Badge key={cat} variant="info" className="text-sm px-3 py-1">
                  {cat}
                </Badge>
              ))}
            </div>

            {/* Description */}
            <div className="mb-8">
              <h2 className="text-lg font-display font-semibold mb-3 flex items-center gap-2">
                <span className="w-8 h-8 rounded-lg bg-primary/10 flex items-center justify-center">
                  📝
                </span>
                Описание
              </h2>
              <p className="text-muted-foreground leading-relaxed text-lg whitespace-pre-line">
                {place.description}
              </p>
            </div>

            {/* Location Info */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-8">
              {place.address && (
                <div className="flex items-start gap-3">
                  <div className="w-10 h-10 rounded-lg bg-secondary/10 flex items-center justify-center flex-shrink-0">
                    <MapPin className="h-5 w-5 text-secondary" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-foreground mb-1">Адрес</h3>
                    <p className="text-muted-foreground">{place.address}</p>
                  </div>
                </div>
              )}

              {place.latitude && place.longitude && (
                <div className="flex items-start gap-3">
                  <div className="w-10 h-10 rounded-lg bg-secondary/10 flex items-center justify-center flex-shrink-0">
                    <Map className="h-5 w-5 text-secondary" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-foreground mb-1">
                      Расположение
                    </h3>
                    <p className="text-muted-foreground text-sm mb-2">
                      {place.latitude.toFixed(4)}, {place.longitude.toFixed(4)}
                    </p>
                    <Button variant="outline" size="sm" onClick={openMap}>
                      <ExternalLink className="h-3.5 w-3.5 mr-2" />
                      Открыть на карте
                    </Button>
                  </div>
                </div>
              )}
            </div>

            <hr className="border-border mb-6" />

            {/* Admin Actions Section */}
            {isAdmin(user?.role) && (
              <div className="mb-6 p-4 rounded-lg border border-warning/20 bg-warning/5">
                <div className="flex items-center gap-3 flex-wrap">
                  <span className="text-sm font-semibold text-foreground">Админ-панель:</span>
                  <Button
                    variant="outline"
                    onClick={handleEdit}
                    className="gap-2"
                  >
                    <Edit className="h-4 w-4" />
                    Редактировать место
                  </Button>
                  <Button
                    variant="destructive"
                    onClick={handleDelete}
                    className="gap-2"
                  >
                    <Trash2 className="h-4 w-4" />
                    Удалить место
                  </Button>
                </div>
              </div>
            )}

            {/* Actions */}
            <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
              <Badge variant="likes" className="text-base">
                <Heart className="h-4 w-4 mr-1.5 fill-current" />
                {place.likes} лайков
              </Badge>

              <div className="flex items-center gap-3 flex-wrap">
                <Button
                  variant={place.isFavorite ? "favorite" : "favorite-outline"}
                  onClick={handleFavorite}
                  className="gap-2"
                  disabled={!user}
                >
                  <Star className={cn("h-4 w-4", place.isFavorite && "fill-current")} />
                  {place.isFavorite ? "В избранном" : "В избранное"}
                </Button>
                <Button
                  variant={place.isLiked ? "like" : "like-outline"}
                  onClick={handleLike}
                  className="gap-2"
                  disabled={!user}
                >
                  <Heart className={cn("h-4 w-4", place.isLiked && "fill-current")} />
                  {place.isLiked ? "Лайкнуто" : "Лайкнуть"}
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Edit Dialog */}
        <EditPlaceDialog
          place={place}
          open={editDialogOpen}
          onOpenChange={setEditDialogOpen}
          onSave={handlePlaceUpdated}
          categories={categories}
        />
      </div>
    </Layout>
  );
};

export default PlaceDetail;
