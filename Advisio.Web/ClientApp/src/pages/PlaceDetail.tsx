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
  Trash2,
  Bell,
  MessageSquare,
  FolderPlus,
  CheckCircle2
} from "lucide-react";
import { cn, isAdmin } from "@/lib/utils";
import { Textarea } from "@/components/ui/textarea";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { toast } from "sonner";
import { useAuth } from "@/contexts/AuthContext";
import EditPlaceDialog from "@/components/EditPlaceDialog";
import type { Place } from "@/components/PlaceCard";
import { useResolvingImageUrl } from "@/hooks/useResolvingImageUrl";

const AddReviewForm = ({ onSubmit, loading }: { onSubmit: (rating: number, comment: string) => void; loading: boolean }) => {
  const [rating, setRating] = useState(0);
  const [comment, setComment] = useState("");
  return (
    <form onSubmit={(e) => { e.preventDefault(); if (rating >= 1) onSubmit(rating, comment); setComment(""); setRating(0); }} className="space-y-3">
      <div className="flex gap-1">
        {[1,2,3,4,5].map((s) => (
          <button key={s} type="button" onClick={() => setRating(s)} className="p-1">
            <Star className={cn("h-8 w-8 transition-colors", s <= rating ? "fill-warning text-warning" : "text-muted-foreground hover:text-warning")} />
          </button>
        ))}
      </div>
      <Textarea placeholder="Комментарий к оценке (необязательно)" value={comment} onChange={(e) => setComment(e.target.value)} rows={2} className="bg-background" />
      <Button type="submit" disabled={rating < 1 || loading}>{loading ? "Отправка..." : "Оценить"}</Button>
    </form>
  );
};

const AddCommentForm = ({ onSubmit, loading }: { onSubmit: (comment: string) => void; loading: boolean }) => {
  const [comment, setComment] = useState("");
  return (
    <form onSubmit={(e) => { e.preventDefault(); if (comment.trim()) { onSubmit(comment.trim()); setComment(""); } }} className="flex gap-2">
      <Textarea placeholder="Добавить комментарий..." value={comment} onChange={(e) => setComment(e.target.value)} rows={2} className="bg-background flex-1" />
      <Button type="submit" disabled={!comment.trim() || loading}>{loading ? "..." : "Отправить"}</Button>
    </form>
  );
};

const PlaceDetail = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  const [place, setPlace] = useState<Place | null>(null);
  const [loading, setLoading] = useState(true);
  const [editDialogOpen, setEditDialogOpen] = useState(false);
  const [categories, setCategories] = useState<Array<{ id: number; name: string }>>([]);
  const [reviews, setReviews] = useState<Array<{ id: number; userId: number; username: string; rating: number; comment?: string; createdAt: string }>>([]);
  const [comments, setComments] = useState<Array<{ id: number; userId: number; username: string; comment: string; createdAt: string }>>([]);
  const [reviewLoading, setReviewLoading] = useState(false);
  const [commentLoading, setCommentLoading] = useState(false);
  const [collections, setCollections] = useState<Array<{ id: number; name: string; places: { id: number }[] }>>([]);
  const [isVisited, setIsVisited] = useState(false);
  const { effectiveUrl: coverImageUrl, onImgError: onCoverImgError } = useResolvingImageUrl(place?.imageUrl);

  useEffect(() => {
    if (id) {
      loadPlace(parseInt(id));
    }
    loadCategories();
    loadCollections();
  }, [id, user?.id]);

  useEffect(() => {
    if (place && user) {
      fetch(`/api/Places/${place.id}/view?userId=${user.id}`, { method: "POST", credentials: "include" }).catch(() => {});
    }
  }, [place?.id, user?.id]);

  const loadReviews = async (placeId: number) => {
    try {
      const [revRes, comRes] = await Promise.all([
        fetch(`/api/Places/${placeId}/reviews`),
        fetch(`/api/Places/${placeId}/comments`),
      ]);
      if (revRes.ok) setReviews(await revRes.json());
      if (comRes.ok) setComments(await comRes.json());
    } catch (e) {
      console.error("Failed to load reviews/comments:", e);
    }
  };

  const loadCollections = async () => {
    if (!user) return;
    try {
      const res = await fetch(`/api/Collections?userId=${user.id}`);
      if (res.ok) {
        const data = await res.json();
        setCollections(data.map((c: any) => ({ id: c.id, name: c.name, places: (c.places || []).map((p: any) => ({ id: p.id })) })));
      }
    } catch (e) {
      console.error("Failed to load collections:", e);
    }
  };

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
          latitude: data.latitude != null ? Number(data.latitude) : undefined,
          longitude: data.longitude != null ? Number(data.longitude) : undefined,
          likes: data.likesCount || 0,
          categories: (data.categories || []).map((c: { name?: string } | string) =>
            typeof c === "string" ? c : c?.name || ""
          ).filter(Boolean),
          isLiked: data.isLiked || false,
          isFavorite: data.isFavorite || false,
          averageRating: data.averageRating,
          reviewsCount: data.reviewsCount || 0,
          isSubscribed: data.isSubscribed || false,
          isVisited: data.isVisited || false,
        });
        setIsVisited(data.isVisited || false);
        loadReviews(placeId);
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
    // Используем поиск по названию и адресу для точного местоположения (координаты могут быть приблизительными)
    const searchQuery = [place?.name, place?.address].filter(Boolean).join(", ");
    if (searchQuery) {
      const encoded = encodeURIComponent(searchQuery);
      window.open(`https://www.google.com/maps/search/?api=1&query=${encoded}`, "_blank");
    } else if (place?.latitude != null && place?.longitude != null) {
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

  const handleMarkVisited = async () => {
    if (!place || !user) {
      toast.error("Необходимо войти в систему");
      return;
    }
    try {
      const response = await fetch(`/api/Places/${place.id}/visited?userId=${user.id}`, {
        method: isVisited ? "DELETE" : "POST",
        credentials: "include",
      });
      if (response.ok) {
        const newVal = !isVisited;
        setIsVisited(newVal);
        setPlace((p) => p ? { ...p, isVisited: newVal } : null);
        toast.success(isVisited ? "Метка о посещении снята" : "Место отмечено как посещённое");
      } else {
        const err = await response.json().catch(() => ({}));
        toast.error(err?.error || "Не удалось обновить");
      }
    } catch (e) {
      toast.error("Ошибка при обновлении");
    }
  };

  const handleSubscribe = async () => {
    if (!place || !user) {
      toast.error("Необходимо войти в систему");
      return;
    }
    try {
      const isSub = place.isSubscribed;
      const response = await fetch(`/api/Places/${place.id}/subscribe?userId=${user.id}`, {
        method: isSub ? "DELETE" : "POST",
        credentials: "include",
      });
      if (response.ok) {
        setPlace((p) => p ? { ...p, isSubscribed: !isSub } : null);
          toast.success(isSub ? "Подписка отменена" : "Вы подписались на обновления");
      }
    } catch (e) {
      toast.error("Ошибка при подписке");
    }
  };

  const handleAddToCollection = async (collectionId: number) => {
    if (!place || !user) return;
    try {
      const res = await fetch(`/api/Collections/${collectionId}/places/${place.id}?userId=${user.id}`, {
        method: "POST",
        credentials: "include",
      });
      if (res.ok) {
        toast.success("Место добавлено в коллекцию");
        loadCollections();
      } else {
        toast.error("Место уже в коллекции или ошибка");
      }
    } catch (e) {
      toast.error("Ошибка при добавлении");
    }
  };

  const handleAddReview = async (rating: number, comment: string) => {
    if (!place || !user) return;
    setReviewLoading(true);
    try {
      const res = await fetch(`/api/Places/${place.id}/reviews?userId=${user.id}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ rating, comment: comment || null }),
      });
      if (res.ok) {
        const newReview = await res.json();
        setReviews((prev) => [newReview, ...prev]);
        setPlace((p) => {
          if (!p) return null;
          const count = (p.reviewsCount || 0) + 1;
          const prevAvg = (p.averageRating || 0) * (count - 1);
          const avg = (prevAvg + rating) / count;
          return { ...p, reviewsCount: count, averageRating: avg };
        });
        toast.success("Отзыв добавлен");
      } else {
        const errData = await res.json().catch(() => ({}));
        toast.error(errData?.error || errData?.title || errData?.message || "Не удалось добавить отзыв");
      }
    } catch (e) {
      toast.error("Ошибка при добавлении отзыва");
    } finally {
      setReviewLoading(false);
    }
  };

  const handleAddComment = async (comment: string) => {
    if (!place || !user) return;
    setCommentLoading(true);
    try {
      const res = await fetch(`/api/Places/${place.id}/comments?userId=${user.id}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ comment }),
      });
      if (res.ok) {
        const newComment = await res.json();
        setComments((prev) => [newComment, ...prev]);
        toast.success("Комментарий добавлен");
      } else {
        const errData = await res.json().catch(() => ({}));
        toast.error(errData?.error || "Не удалось добавить комментарий");
      }
    } catch (e) {
      toast.error("Ошибка при добавлении комментария");
    } finally {
      setCommentLoading(false);
    }
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
          {coverImageUrl ? (
            <div className="relative h-64 md:h-96 overflow-hidden">
              <img
                src={coverImageUrl}
                alt={place.name}
                className="w-full h-full object-cover"
                onError={onCoverImgError}
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

              {(place.latitude != null && place.longitude != null) && (
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
              {(place.name || place.address) && (!place.latitude || !place.longitude) && (
                <div className="flex items-start gap-3">
                  <div className="w-10 h-10 rounded-lg bg-secondary/10 flex items-center justify-center flex-shrink-0">
                    <Map className="h-5 w-5 text-secondary" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-foreground mb-1">На карте</h3>
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
              <div className="flex items-center gap-3 flex-wrap">
                <Badge variant="likes" className="text-base">
                  <Heart className="h-4 w-4 mr-1.5 fill-current" />
                  {place.likes} лайков
                </Badge>
                {place.averageRating != null && (
                  <Badge variant="secondary" className="text-base">
                    <Star className="h-4 w-4 mr-1.5 fill-current text-warning" />
                    {place.averageRating.toFixed(1)} ({place.reviewsCount || 0})
                  </Badge>
                )}
              </div>

              <div className="flex items-center gap-3 flex-wrap">
                {user && collections.length > 0 && (
                  <Popover>
                    <PopoverTrigger asChild>
                      <Button variant="outline" className="gap-2">
                        <FolderPlus className="h-4 w-4" />
                        Добавить в коллекцию
                      </Button>
                    </PopoverTrigger>
                    <PopoverContent className="w-56 p-2">
                      {collections.map((col) => {
                        const hasPlace = col.places.some((p) => p.id === place.id);
                        return (
                          <button
                            key={col.id}
                            className="w-full text-left px-3 py-2 rounded hover:bg-muted text-sm disabled:opacity-50"
                            disabled={hasPlace}
                            onClick={() => handleAddToCollection(col.id)}
                          >
                            {col.name} {hasPlace && "(уже добавлено)"}
                          </button>
                        );
                      })}
                    </PopoverContent>
                  </Popover>
                )}
                {user && (
                  <Button
                    type="button"
                    variant={isVisited ? "default" : "outline"}
                    onClick={handleMarkVisited}
                    className="gap-2"
                  >
                    <CheckCircle2 className={cn("h-4 w-4", isVisited && "fill-current")} />
                    {isVisited ? "Посещено" : "Отметить посещённым"}
                  </Button>
                )}
                {user && (
                  <Button
                    variant={place.isSubscribed ? "default" : "outline"}
                    onClick={handleSubscribe}
                    className="gap-2"
                  >
                    <Bell className={cn("h-4 w-4", place.isSubscribed && "fill-current")} />
                    {place.isSubscribed ? "Подписка активна" : "Подписаться на обновления"}
                  </Button>
                )}
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

            {/* Reviews and Comments */}
            <div className="mt-8 pt-6 border-t border-border">
              <h2 className="text-lg font-display font-semibold mb-4 flex items-center gap-2">
                <MessageSquare className="h-5 w-5" />
                Рейтинги и комментарии ({reviews.length + comments.length})
              </h2>
              {user && !reviews.some((r) => r.userId === user.id) && (
                <div className="mb-4 p-4 rounded-lg border border-border bg-muted/20">
                  <p className="text-sm font-medium mb-2">Поставить оценку (один раз)</p>
                  <AddReviewForm onSubmit={handleAddReview} loading={reviewLoading} />
                </div>
              )}
              {user && reviews.some((r) => r.userId === user.id) && (
                <p className="mb-2 text-sm text-muted-foreground">Вы уже оценили это место</p>
              )}
              {user && (
                <div className="mb-4 p-4 rounded-lg border border-border bg-muted/20">
                  <p className="text-sm font-medium mb-2">Добавить комментарий</p>
                  <AddCommentForm onSubmit={handleAddComment} loading={commentLoading} />
                </div>
              )}
              <div className="mt-4 space-y-3">
                {reviews.map((r) => (
                  <div key={`r-${r.id}`} className="p-4 rounded-lg border border-border bg-muted/30">
                    <div className="flex items-center justify-between mb-2">
                      <span className="font-medium">{r.username}</span>
                      <div className="flex items-center gap-1">
                        {[1,2,3,4,5].map((s) => (
                          <Star key={s} className={cn("h-4 w-4", s <= r.rating ? "fill-warning text-warning" : "text-muted-foreground")} />
                        ))}
                      </div>
                    </div>
                    {r.comment && <p className="text-sm text-muted-foreground">{r.comment}</p>}
                  </div>
                ))}
                {comments.map((c) => (
                  <div key={`c-${c.id}`} className="p-4 rounded-lg border border-border bg-muted/20">
                    <div className="flex items-center justify-between mb-2">
                      <span className="font-medium">{c.username}</span>
                    </div>
                    <p className="text-sm text-muted-foreground">{c.comment}</p>
                  </div>
                ))}
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
          isAdmin={isAdmin(user?.role)}
        />
      </div>
    </Layout>
  );
};

export default PlaceDetail;
