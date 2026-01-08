import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import Layout from "@/components/Layout";
import { Card, CardContent, CardHeader, CardFooter } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Heart, Star, Sparkles, MapPin, ExternalLink } from "lucide-react";
import PlaceCard from "@/components/PlaceCard";
import { cn } from "@/lib/utils";
import { toast } from "sonner";
import { useAuth } from "@/contexts/AuthContext";
import type { Place } from "@/components/PlaceCard";

interface Recommendation {
  place: Place;
  score: number;
  reason?: string;
}

const Recommendations = () => {
  const { user, isAuthenticated } = useAuth();
  const [recommendations, setRecommendations] = useState<Recommendation[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (isAuthenticated) {
      loadRecommendations();
    }
  }, [isAuthenticated]);

  const loadRecommendations = async () => {
    if (!user) return;
    setLoading(true);
    try {
      const response = await fetch(`/api/Recommendations?userId=${user.id}&count=9`);
      if (response.ok) {
        const data = await response.json();
        const recommendationsData: Recommendation[] = data.map((r: any) => ({
          place: {
            id: r.place?.id || r.id,
            name: r.place?.name || r.name || "Без названия",
            description: r.place?.description || r.description || "",
            type: r.place?.type || r.type || null,
            address: r.place?.address || r.address || null,
            imageUrl: r.place?.imageUrl || r.imageUrl || null,
            latitude: r.place?.latitude || r.latitude ? Number(r.place?.latitude || r.latitude) : null,
            longitude: r.place?.longitude || r.longitude ? Number(r.place?.longitude || r.longitude) : null,
            likes: r.place?.likesCount || r.likesCount || 0,
            categories: r.place?.categories || r.categories || [],
            isLiked: r.place?.isLiked || r.isLiked || false,
            isFavorite: r.place?.isFavorite || r.isFavorite || false,
          },
          score: r.score || 0,
          reason: r.reason || null,
        }));
        setRecommendations(recommendationsData);
      } else {
        toast.error("Не удалось загрузить рекомендации");
      }
    } catch (error) {
      console.error("Failed to load recommendations:", error);
      toast.error("Ошибка при загрузке рекомендаций");
    } finally {
      setLoading(false);
    }
  };

  const handleLike = async (id: number) => {
    if (!user) {
      toast.error("Необходимо войти в систему");
      return;
    }
    try {
      const recommendation = recommendations.find((r) => r.place.id === id);
      const isLiked = recommendation?.place.isLiked;

      const response = await fetch(`/api/Places/${id}/likes?userId=${user.id}`, {
        method: isLiked ? "DELETE" : "POST",
      });

      if (response.ok) {
        setRecommendations((prev) =>
          prev.map((rec) =>
            rec.place.id === id
              ? {
                  ...rec,
                  place: {
                    ...rec.place,
                    isLiked: !rec.place.isLiked,
                    likes: rec.place.isLiked
                      ? rec.place.likes - 1
                      : rec.place.likes + 1,
                  },
                }
              : rec
          )
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

  const handleFavorite = async (id: number) => {
    if (!user) {
      toast.error("Необходимо войти в систему");
      return;
    }
    try {
      const recommendation = recommendations.find((r) => r.place.id === id);
      const isFavorite = recommendation?.place.isFavorite;

      const response = await fetch(`/api/Places/${id}/favorites?userId=${user.id}`, {
        method: isFavorite ? "DELETE" : "POST",
      });

      if (response.ok) {
        setRecommendations((prev) =>
          prev.map((rec) =>
            rec.place.id === id
              ? {
                  ...rec,
                  place: { ...rec.place, isFavorite: !rec.place.isFavorite },
                }
              : rec
          )
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

  if (!isAuthenticated) {
    return (
      <Layout>
        <div className="container py-16">
          <div className="max-w-md mx-auto text-center">
            <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-warning/20 flex items-center justify-center">
              <Sparkles className="h-8 w-8 text-warning" />
            </div>
            <h1 className="text-2xl font-display font-bold mb-4">
              Требуется авторизация
            </h1>
            <p className="text-muted-foreground mb-6">
              Необходимо войти в систему для получения рекомендаций
            </p>
            <Link to="/login">
              <Button size="lg">Войти в систему</Button>
            </Link>
          </div>
        </div>
      </Layout>
    );
  }

    return (
      <Layout>
      <div className="container py-8 md:py-12">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl md:text-4xl font-display font-bold text-foreground mb-2">
            Персональные рекомендации
          </h1>
          <p className="text-muted-foreground">
            Места, подобранные специально для вас
          </p>
        </div>

        {loading ? (
          <div className="text-center py-16">
            <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-muted flex items-center justify-center animate-pulse">
              <Sparkles className="h-8 w-8 text-muted-foreground" />
            </div>
            <p className="text-muted-foreground">Загрузка рекомендаций...</p>
          </div>
        ) : recommendations.length > 0 ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {recommendations.map((rec, index) => (
              <Card
                key={rec.place.id}
                variant="recommendation"
                className="flex flex-col animate-slide-up opacity-0"
                style={{
                  animationDelay: `${index * 0.1}s`,
                  animationFillMode: "forwards",
                }}
              >
                {/* Score Header */}
                <CardHeader className="bg-success text-success-foreground py-3 px-6">
                  <div className="flex items-center justify-between">
                    <span className="font-semibold">
                      Оценка: {rec.score.toFixed(1)}
                    </span>
                    <Sparkles className="h-5 w-5" />
                  </div>
                </CardHeader>

                {/* Image */}
                {rec.place.imageUrl ? (
                  <div className="relative h-48 overflow-hidden">
                    <img
                      src={rec.place.imageUrl}
                      alt={rec.place.name}
                      className="w-full h-full object-cover transition-transform duration-500 hover:scale-105"
                    />
                  </div>
                ) : (
                  <div className="h-48 bg-muted flex items-center justify-center">
                    <MapPin className="h-12 w-12 text-muted-foreground/50" />
                  </div>
                )}

                <CardContent className="flex-1 pt-5">
                  <h3 className="font-display text-xl font-semibold mb-2 text-foreground">
                    {rec.place.name}
                  </h3>

                  <p className="text-sm text-muted-foreground mb-3 line-clamp-2">
                    {rec.place.description.substring(0, 100)}...
                  </p>

                  {rec.reason && (
                    <p className="text-sm text-muted-foreground italic mb-3">
                      {rec.reason}
                    </p>
                  )}

                  <div className="flex flex-wrap gap-2">
                    {rec.place.type && <Badge variant="type">{rec.place.type}</Badge>}
                    {rec.place.categories.slice(0, 2).map((cat) => (
                      <Badge key={cat} variant="category">
                        {cat}
                      </Badge>
                    ))}
                  </div>
                </CardContent>

                <CardFooter className="flex items-center justify-between pt-4 border-t border-border/50">
                  <span className="text-sm text-muted-foreground flex items-center gap-1">
                    <Heart className="h-4 w-4" />
                    {rec.place.likes}
                  </span>

                  <div className="flex items-center gap-2">
                    <Button
                      variant={rec.place.isFavorite ? "favorite" : "favorite-outline"}
                      size="icon"
                      onClick={() => handleFavorite(rec.place.id)}
                      className="h-9 w-9"
                    >
                      <Star
                        className={cn(
                          "h-4 w-4",
                          rec.place.isFavorite && "fill-current"
                        )}
                      />
                    </Button>
                    <Button
                      variant={rec.place.isLiked ? "like" : "like-outline"}
                      size="icon"
                      onClick={() => handleLike(rec.place.id)}
                      className="h-9 w-9"
                    >
                      <Heart
                        className={cn(
                          "h-4 w-4",
                          rec.place.isLiked && "fill-current"
                        )}
                      />
                    </Button>
                  </div>
                </CardFooter>

                <div className="px-6 pb-5">
                  <Link to={`/place/${rec.place.id}`}>
                    <Button variant="default" className="w-full gap-2" size="sm">
                      Подробнее
                      <ExternalLink className="h-3.5 w-3.5" />
                    </Button>
                  </Link>
                </div>
              </Card>
            ))}
          </div>
        ) : (
          <div className="text-center py-16">
            <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-muted flex items-center justify-center">
              <Sparkles className="h-8 w-8 text-muted-foreground" />
            </div>
            <h3 className="text-xl font-display font-semibold mb-2">
              Рекомендации не найдены
            </h3>
            <p className="text-muted-foreground mb-6">
              Пройдите опрос для получения персональных рекомендаций
            </p>
            <Link to="/survey">
              <Button size="lg">Пройти опрос</Button>
            </Link>
          </div>
        )}
      </div>
    </Layout>
  );
};

export default Recommendations;
