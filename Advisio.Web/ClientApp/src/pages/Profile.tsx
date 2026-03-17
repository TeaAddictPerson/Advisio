import { useState, useEffect, useCallback } from "react";
import { Link, useNavigate } from "react-router-dom";
import Layout from "@/components/Layout";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Eye, EyeOff, User, Heart, Star, LogOut, FolderOpen, BarChart3, Bell, MapPin, TrendingUp, Camera, ChevronLeft, ChevronRight } from "lucide-react";
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from "recharts";
import useEmblaCarousel from "embla-carousel-react";
import { useAuth } from "@/contexts/AuthContext";
import { toast } from "sonner";
import { isAdmin } from "@/lib/utils";
import type { Place } from "@/components/PlaceCard";

const PublicNotesGallery = ({ notes }: { notes: Array<{ collectionItemId: number; placeId: number; placeName: string; note?: string; photoUrls: string[] }> }) => {
  const [emblaRef, emblaApi] = useEmblaCarousel({ loop: true, align: "start", slidesToScroll: 3 });
  const [canPrev, setCanPrev] = useState(false);
  const [canNext, setCanNext] = useState(false);

  const scrollPrev = useCallback(() => emblaApi?.scrollPrev(), [emblaApi]);
  const scrollNext = useCallback(() => emblaApi?.scrollNext(), [emblaApi]);

  useEffect(() => {
    if (!emblaApi) return;
    setCanPrev(emblaApi.canScrollPrev());
    setCanNext(emblaApi.canScrollNext());
    emblaApi.on("select", () => {
      setCanPrev(emblaApi.canScrollPrev());
      setCanNext(emblaApi.canScrollNext());
    });
  }, [emblaApi]);

  return (
    <Card variant="default" className="mt-6 animate-slide-up overflow-hidden" style={{ animationDelay: "0.29s" }}>
      <CardHeader className="flex flex-row items-center justify-between">
        <div>
          <CardTitle className="flex items-center gap-2 text-lg">
            <Camera className="h-5 w-5 text-primary" />
            Публичные заметки
          </CardTitle>
          <p className="text-sm text-muted-foreground mt-1">Фотокарточки из ваших коллекций</p>
        </div>
        {notes.length > 3 && (
          <div className="flex gap-2">
            <Button variant="outline" size="icon" onClick={scrollPrev} disabled={!canPrev}>
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <Button variant="outline" size="icon" onClick={scrollNext} disabled={!canNext}>
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        )}
      </CardHeader>
      <CardContent>
        <div className="overflow-hidden" ref={emblaRef}>
          <div className="flex gap-4 -ml-4">
            {notes.map((n) => (
              <Link
                key={n.collectionItemId}
                to={`/place/${n.placeId}`}
                className="flex-[0_0_calc(33.333%-1rem)] min-w-0 ml-4 group"
              >
                <div className="rounded-xl overflow-hidden border border-border bg-card hover:border-primary/50 transition-all aspect-[3/4] min-h-[280px] flex flex-col">
                  <div className="flex-1 relative min-h-[180px] bg-muted">
                    {n.photoUrls.length > 0 ? (
                      <img
                        src={n.photoUrls[0]}
                        alt=""
                        className="w-full h-full object-cover"
                      />
                    ) : (
                      <div className="w-full h-full flex items-center justify-center bg-muted">
                        <Camera className="h-16 w-16 text-muted-foreground/50" />
                      </div>
                    )}
                  </div>
                  <div className="p-3 flex flex-col gap-1">
                    {n.note && (
                      <p className="text-sm text-muted-foreground line-clamp-2">{n.note}</p>
                    )}
                    <span className="font-semibold text-sm">{n.placeName}</span>
                  </div>
                </div>
              </Link>
            ))}
          </div>
        </div>
      </CardContent>
    </Card>
  );
};

const Profile = () => {
  const navigate = useNavigate();
  const { user, isAuthenticated, isLoading, logout, updateProfile } = useAuth();
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [favorites, setFavorites] = useState<Place[]>([]);
  const [liked, setLiked] = useState<Place[]>([]);
  const [loadingPlaces, setLoadingPlaces] = useState(true);
  const [collections, setCollections] = useState<Array<{ id: number; name: string; description?: string; places: Place[] }>>([]);
  const [stats, setStats] = useState<{
    mostViewed: Array<{ place: Place; viewCount: number }>;
    topFilters: Array<{ filterType: string; filterValue: string; usageCount: number }>;
    subscriptions?: Place[];
    visitedPlaces?: Place[];
    topCategories?: Array<{ categoryName: string; type?: string; visitCount: number; placesCount: number }>;
    topPlaceTypes?: Array<{ typeName: string; viewCount: number; placesCount: number }>;
    activityByDate?: Array<{ date: string; views: number }>;
    publicNotes?: Array<{ collectionItemId: number; placeId: number; placeName: string; note?: string; photoUrls: string[] }>;
  } | null>(null);
  const [createCollectionOpen, setCreateCollectionOpen] = useState(false);
  const [newCollectionName, setNewCollectionName] = useState("");
  const [newCollectionDesc, setNewCollectionDesc] = useState("");

  useEffect(() => {
    if (isAuthenticated && user) {
      setUsername(user.username);
      setEmail(user.email);
      setCurrentPassword("");
      setNewPassword("");
      setFavorites([]);
      setLiked([]);
      setCollections([]);
      setStats(null);
      loadUserPlaces();
      loadCollections();
      loadStats();
    } else {
      // Сбрасываем состояние при выходе
      setUsername("");
      setEmail("");
      setCurrentPassword("");
      setNewPassword("");
      setFavorites([]);
      setLiked([]);
    }
  }, [isAuthenticated, user?.id]); // Используем user?.id для отслеживания смены пользователя

  const loadUserPlaces = async () => {
    if (!user) return;
    setLoadingPlaces(true);
    try {
      // Load favorites using dedicated endpoint
      const favResponse = await fetch(`/api/Places/favorites/${user.id}`);
      if (favResponse.ok) {
        const favoritesData = await favResponse.json();
        setFavorites(favoritesData.map((p: any) => ({
          id: p.id,
          name: p.name || "Без названия",
          description: p.description || "",
          type: p.type || null,
          address: p.address || null,
          imageUrl: p.imageUrl || null,
          latitude: p.latitude ? Number(p.latitude) : null,
          longitude: p.longitude ? Number(p.longitude) : null,
          likes: p.likesCount || 0,
          categories: p.categories?.map((c: { name: string }) => c.name) || [],
          isLiked: p.isLiked || false,
          isFavorite: true,
        })));
      }

      // Load liked using dedicated endpoint
      const likedResponse = await fetch(`/api/Places/liked/${user.id}`);
      if (likedResponse.ok) {
        const likedData = await likedResponse.json();
        setLiked(likedData.map((p: any) => ({
          id: p.id,
          name: p.name || "Без названия",
          description: p.description || "",
          type: p.type || null,
          address: p.address || null,
          imageUrl: p.imageUrl || null,
          latitude: p.latitude ? Number(p.latitude) : null,
          longitude: p.longitude ? Number(p.longitude) : null,
          likes: p.likesCount || 0,
          categories: p.categories?.map((c: { name: string }) => c.name) || [],
          isLiked: true,
          isFavorite: p.isFavorite || false,
        })));
      }
    } catch (error) {
      console.error("Failed to load user places:", error);
    } finally {
      setLoadingPlaces(false);
    }
  };

  const loadCollections = async () => {
    if (!user) return;
    try {
      const res = await fetch(`/api/Collections?userId=${user.id}`);
      if (res.ok) {
        const data = await res.json();
        setCollections(data.map((c: any) => ({
          id: c.id,
          name: c.name,
          description: c.description,
          places: (c.places || []).map((p: any) => ({
            id: p.id,
            name: p.name || "Без названия",
            description: p.description || "",
            type: p.type || null,
            address: p.address || null,
            imageUrl: p.imageUrl || null,
            latitude: p.latitude ? Number(p.latitude) : null,
            longitude: p.longitude ? Number(p.longitude) : null,
            likes: p.likesCount || 0,
            categories: p.categories?.map((x: { name: string }) => x.name) || [],
            isLiked: p.isLiked || false,
            isFavorite: p.isFavorite || false,
          })),
        })));
      }
    } catch (e) {
      console.error("Failed to load collections:", e);
    }
  };

  const loadStats = async () => {
    if (!user) return;
    try {
      const res = await fetch(`/api/Users/${user.id}/stats`, { credentials: "include" });
      if (res.ok) {
        const data = await res.json();
        const mapPlace = (p: any) => ({
          id: p.id,
          name: p.name || "Без названия",
          description: p.description || "",
          type: p.type || null,
          address: p.address || null,
          imageUrl: p.imageUrl || null,
          latitude: p.latitude ? Number(p.latitude) : null,
          longitude: p.longitude ? Number(p.longitude) : null,
          likes: p.likesCount || 0,
          categories: p.categories?.map((c: { name: string }) => c.name) || [],
          isLiked: p.isLiked || false,
          isFavorite: p.isFavorite || false,
        });
        setStats({
          mostViewed: (data.mostViewedPlaces || []).map((m: any) => ({ place: mapPlace(m.place), viewCount: m.viewCount })),
          topFilters: (data.topFilters || []).map((f: any) => ({ filterType: f.filterType, filterValue: f.filterValue, usageCount: f.usageCount })),
          subscriptions: (data.subscriptions || []).map(mapPlace),
          visitedPlaces: (data.visitedPlaces || []).map(mapPlace),
          topCategories: (data.topCategories || []).map((c: any) => ({ categoryName: c.categoryName, type: c.type, visitCount: c.visitCount, placesCount: c.placesCount })),
          topPlaceTypes: (data.topPlaceTypes || []).map((t: any) => ({ typeName: t.typeName, viewCount: t.viewCount, placesCount: t.placesCount })),
          activityByDate: (data.activityByDate || []).map((a: any) => ({ date: a.date, views: a.views })),
          publicNotes: (data.publicNotes || []).map((n: any) => ({ collectionItemId: n.collectionItemId, placeId: n.placeId, placeName: n.placeName, note: n.note, photoUrls: n.photoUrls || [] })),
        });
      }
    } catch (e) {
      console.error("Failed to load stats:", e);
    }
  };

  const handleCreateCollection = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!user || !newCollectionName.trim()) return;
    try {
      const res = await fetch(`/api/Collections?userId=${user.id}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ name: newCollectionName.trim(), description: newCollectionDesc.trim() || null }),
      });
      if (res.ok) {
        toast.success("Коллекция создана");
        setCreateCollectionOpen(false);
        setNewCollectionName("");
        setNewCollectionDesc("");
        loadCollections();
      } else {
        toast.error("Не удалось создать коллекцию");
      }
    } catch (e) {
      toast.error("Ошибка при создании коллекции");
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    
    // Админы не могут редактировать профиль
    if (isAdmin(user?.role)) {
      toast.error("Администраторы не могут редактировать свой профиль");
      return;
    }
    
    setLoading(true);
    
    const success = await updateProfile(
      username,
      email,
      newPassword || undefined,
      currentPassword || undefined
    );
    
    if (success) {
      setCurrentPassword("");
      setNewPassword("");
    }
    
    setLoading(false);
  };

  const handleLogout = async () => {
    await logout();
    navigate("/");
  };

  if (isLoading) {
    return (
      <Layout>
        <div className="container py-16 text-center">
          <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-muted flex items-center justify-center animate-pulse">
            <User className="h-8 w-8 text-muted-foreground" />
          </div>
          <p className="text-muted-foreground">Проверяем авторизацию...</p>
        </div>
      </Layout>
    );
  }

  if (!isAuthenticated) {
    return (
      <Layout>
        <div className="container py-16">
          <div className="max-w-md mx-auto text-center">
            <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-warning/20 flex items-center justify-center">
              <User className="h-8 w-8 text-warning" />
            </div>
            <h1 className="text-2xl font-display font-bold mb-4">
              Требуется авторизация
            </h1>
            <p className="text-muted-foreground mb-6">
              Необходимо войти в систему для просмотра профиля
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
        <div className="max-w-4xl mx-auto">
          {/* Header Card */}
          <Card variant="elevated" className="mb-6 animate-fade-in">
            <CardHeader className="flex flex-row items-center justify-between">
              <div className="flex items-center gap-4">
                <div className="w-16 h-16 rounded-full bg-primary/10 flex items-center justify-center">
                  <User className="h-8 w-8 text-primary" />
                </div>
                <div>
                  <CardTitle className="text-2xl">{user?.username}</CardTitle>
                  <p className="text-muted-foreground">{user?.email}</p>
                </div>
              </div>
              <Button variant="outline" onClick={handleLogout} className="gap-2">
                <LogOut className="h-4 w-4" />
                Выйти
              </Button>
            </CardHeader>
          </Card>

          {/* Tabs */}
          <Tabs defaultValue="view" className="animate-slide-up">
            <TabsList className="w-full mb-6">
              <TabsTrigger value="view" className="flex-1">
                Просмотр
              </TabsTrigger>
              {!isAdmin(user?.role) && (
                <TabsTrigger value="edit" className="flex-1">
                  Редактировать
                </TabsTrigger>
              )}
            </TabsList>

            <TabsContent value="view">
              <Card variant="default">
                <CardContent className="py-6 space-y-4">
                  <div className="flex justify-between py-3 border-b border-border">
                    <span className="font-medium">Имя:</span>
                    <span className="text-muted-foreground">{user?.username}</span>
                  </div>
                  <div className="flex justify-between py-3 border-b border-border">
                    <span className="font-medium">Email:</span>
                    <span className="text-muted-foreground">{user?.email}</span>
                  </div>
                  <div className="flex justify-between py-3">
                    <span className="font-medium">Роль:</span>
                    <span className="text-muted-foreground">{isAdmin(user?.role) ? "Администратор" : "Пользователь"}</span>
                  </div>
                </CardContent>
              </Card>
            </TabsContent>

            {(user?.role !== "Admin" && user?.role !== "admin") && (
              <TabsContent value="edit">
                <Card variant="default">
                  <CardContent className="py-6">
                    <form onSubmit={handleSave} className="space-y-4">
                    <div className="space-y-2">
                      <Label htmlFor="edit-username">Имя пользователя</Label>
                      <Input
                        id="edit-username"
                        value={username}
                        onChange={(e) => setUsername(e.target.value)}
                        className="bg-card"
                      />
                    </div>

                    {!isAdmin(user?.role) && (
                      <>
                        <div className="space-y-2">
                          <Label htmlFor="edit-email">Email</Label>
                          <Input
                            id="edit-email"
                            type="email"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                            className="bg-card"
                          />
                        </div>

                        <div className="space-y-2">
                          <Label htmlFor="current-password">
                            Текущий пароль (требуется для смены пароля)
                          </Label>
                          <div className="relative">
                            <Input
                              id="current-password"
                              type={showCurrentPassword ? "text" : "password"}
                              value={currentPassword}
                              onChange={(e) => setCurrentPassword(e.target.value)}
                              className="bg-card pr-10"
                            />
                            <Button
                              type="button"
                              variant="ghost"
                              size="icon"
                              className="absolute right-0 top-0 h-full px-3"
                              onClick={() => setShowCurrentPassword(!showCurrentPassword)}
                            >
                              {showCurrentPassword ? (
                                <EyeOff className="h-4 w-4" />
                              ) : (
                                <Eye className="h-4 w-4" />
                              )}
                            </Button>
                          </div>
                          <p className="text-xs text-muted-foreground">
                            Оставьте пустым, если не хотите менять пароль
                          </p>
                        </div>

                        <div className="space-y-2">
                          <Label htmlFor="new-password">Новый пароль</Label>
                          <div className="relative">
                            <Input
                              id="new-password"
                              type={showNewPassword ? "text" : "password"}
                              value={newPassword}
                              onChange={(e) => setNewPassword(e.target.value)}
                              className="bg-card pr-10"
                            />
                            <Button
                              type="button"
                              variant="ghost"
                              size="icon"
                              className="absolute right-0 top-0 h-full px-3"
                              onClick={() => setShowNewPassword(!showNewPassword)}
                            >
                              {showNewPassword ? (
                                <EyeOff className="h-4 w-4" />
                              ) : (
                                <Eye className="h-4 w-4" />
                              )}
                            </Button>
                          </div>
                        </div>
                      </>
                    )}
                    
                    {isAdmin(user?.role) && (
                      <div className="p-4 rounded-lg border border-warning/20 bg-warning/5">
                        <p className="text-sm text-muted-foreground">
                          Для администраторов изменение email и пароля недоступно
                        </p>
                      </div>
                    )}

                    <Button type="submit" className="w-full" disabled={loading}>
                      {loading ? "Сохранение..." : "Сохранить изменения"}
                    </Button>
                  </form>
                </CardContent>
              </Card>
            </TabsContent>
            )}

          </Tabs>

          {/* Favorites and Likes */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mt-6">
            {/* Favorites */}
            <Card variant="default" className="animate-slide-up" style={{ animationDelay: "0.1s" }}>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-lg">
                  <Star className="h-5 w-5 text-warning" />
                  Избранные места ({favorites.length})
                </CardTitle>
              </CardHeader>
              <CardContent>
                {loadingPlaces ? (
                  <p className="text-muted-foreground text-center py-4">Загрузка...</p>
                ) : favorites.length > 0 ? (
                  <div className="space-y-3">
                    {favorites.map((place) => (
                      <Link
                        key={place.id}
                        to={`/place/${place.id}`}
                        className="block p-3 rounded-lg border border-border hover:border-primary/50 hover:bg-background-light transition-all"
                      >
                        <h4 className="font-medium mb-1">{place.name}</h4>
                        <p className="text-sm text-muted-foreground line-clamp-2">
                          {place.description.substring(0, 100)}...
                        </p>
                      </Link>
                    ))}
                  </div>
                ) : (
                  <p className="text-muted-foreground text-center py-4">
                    У вас пока нет избранных мест
                  </p>
                )}
              </CardContent>
            </Card>

            {/* Liked */}
            <Card variant="default" className="animate-slide-up" style={{ animationDelay: "0.2s" }}>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-lg">
                  <Heart className="h-5 w-5 text-destructive" />
                  Понравившиеся места ({liked.length})
                </CardTitle>
              </CardHeader>
              <CardContent>
                {loadingPlaces ? (
                  <p className="text-muted-foreground text-center py-4">Загрузка...</p>
                ) : liked.length > 0 ? (
                  <div className="space-y-3">
                    {liked.map((place) => (
                      <Link
                        key={place.id}
                        to={`/place/${place.id}`}
                        className="block p-3 rounded-lg border border-border hover:border-primary/50 hover:bg-background-light transition-all"
                      >
                        <h4 className="font-medium mb-1">{place.name}</h4>
                        <p className="text-sm text-muted-foreground line-clamp-2">
                          {place.description.substring(0, 100)}...
                        </p>
                      </Link>
                    ))}
                  </div>
                ) : (
                  <p className="text-muted-foreground text-center py-4">
                    Вы еще не поставили лайки
                  </p>
                )}
              </CardContent>
            </Card>
          </div>

          {/* Subscriptions — всегда показываем блок */}
          <Card variant="default" className="mt-6 animate-slide-up" style={{ animationDelay: "0.25s" }}>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-lg">
                <Bell className="h-5 w-5 text-primary" />
                Подписки ({stats?.subscriptions?.length ?? 0})
              </CardTitle>
              <p className="text-sm text-muted-foreground">Места, на которые вы подписаны — обновления будут видны здесь</p>
            </CardHeader>
            <CardContent>
              {stats?.subscriptions && stats.subscriptions.length > 0 ? (
                <div className="space-y-2">
                  {stats.subscriptions.map((place) => (
                    <Link key={place.id} to={`/place/${place.id}`} className="flex justify-between items-center p-2 rounded hover:bg-muted/50">
                      <span>{place.name}</span>
                      <span className="text-sm text-muted-foreground">→</span>
                    </Link>
                  ))}
                </div>
              ) : (
                <p className="text-muted-foreground text-center py-4">У вас пока нет подписок на места</p>
              )}
            </CardContent>
          </Card>

          {/* Visited Places */}
          <Card variant="default" className="mt-6 animate-slide-up" style={{ animationDelay: "0.28s" }}>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-lg">
                <MapPin className="h-5 w-5 text-success" />
                Посещённые события ({stats?.visitedPlaces?.length ?? 0})
              </CardTitle>
              <p className="text-sm text-muted-foreground">Места, где вы уже побывали</p>
            </CardHeader>
            <CardContent>
              {stats?.visitedPlaces && stats.visitedPlaces.length > 0 ? (
                <div className="space-y-2">
                  {stats.visitedPlaces.map((place) => (
                    <Link key={place.id} to={`/place/${place.id}`} className="block p-2 rounded hover:bg-muted/50">
                      <h4 className="font-medium">{place.name}</h4>
                      <p className="text-sm text-muted-foreground line-clamp-1">{place.description?.substring(0, 80)}...</p>
                    </Link>
                  ))}
                </div>
              ) : (
                <p className="text-muted-foreground text-center py-4">Вы ещё не отметили посещённые места. Откройте страницу места и нажмите «Отметить посещённым»</p>
              )}
            </CardContent>
          </Card>

          {/* Public Notes Gallery */}
          {stats?.publicNotes && stats.publicNotes.length > 0 && (
            <PublicNotesGallery notes={stats.publicNotes} />
          )}

          {/* Collections */}
          <Card variant="default" className="mt-6 animate-slide-up" style={{ animationDelay: "0.3s" }}>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-lg">
                <FolderOpen className="h-5 w-5 text-primary" />
                Мои коллекции
              </CardTitle>
              <p className="text-sm text-muted-foreground">Создавайте коллекции мест, например «Мои планы на выходные»</p>
            </CardHeader>
            <CardContent>
              <Button variant="outline" size="sm" className="mb-4" onClick={() => setCreateCollectionOpen(true)}>
                + Создать коллекцию
              </Button>
              {collections.length === 0 ? (
                <p className="text-muted-foreground text-center py-4">У вас пока нет коллекций. Создайте коллекцию и добавляйте места со страницы места.</p>
              ) : (
                <div className="space-y-4">
                  {collections.map((col) => (
                    <div key={col.id} className="p-4 rounded-lg border border-border">
                      <Link to={`/collection/${col.id}`} className="block hover:opacity-80">
                        <h4 className="font-semibold mb-1">{col.name}</h4>
                      </Link>
                      {col.description && <p className="text-sm text-muted-foreground mb-2">{col.description}</p>}
                      <div className="space-y-2">
                        {col.places.map((place) => (
                          <Link key={place.id} to={`/place/${place.id}`} className="block p-2 rounded hover:bg-muted/50">
                            {place.name}
                          </Link>
                        ))}
                      </div>
                      <Link to={`/collection/${col.id}`} className="text-sm text-primary mt-2 inline-block">
                        Открыть коллекцию →
                      </Link>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          {/* Stats — всегда показываем блок, чтобы были видны «Любимые категории» и др. */}
          {stats && (
            <Card variant="default" className="mt-6 animate-slide-up" style={{ animationDelay: "0.4s" }}>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-lg">
                  <BarChart3 className="h-5 w-5 text-primary" />
                  Статистика активности
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {stats.mostViewed.length > 0 && (
                  <div>
                    <h4 className="font-medium mb-2">Чаще всего просматривали</h4>
                    <div className="space-y-2">
                      {stats.mostViewed.map(({ place, viewCount }) => (
                        <Link key={place.id} to={`/place/${place.id}`} className="flex justify-between items-center p-2 rounded hover:bg-muted/50">
                          <span>{place.name}</span>
                          <span className="text-sm text-muted-foreground">{viewCount} просмотров</span>
                        </Link>
                      ))}
                    </div>
                  </div>
                )}
                {stats.topFilters.length > 0 && (
                  <div>
                    <h4 className="font-medium mb-2">Часто используемые фильтры</h4>
                    <div className="flex flex-wrap gap-2">
                      {stats.topFilters.map((f, i) => (
                        <span key={i} className="px-2 py-1 rounded bg-muted text-sm">
                          {f.filterType}: {f.filterValue} ({f.usageCount})
                        </span>
                      ))}
                    </div>
                  </div>
                )}
                <div>
                  <h4 className="font-medium mb-2">Любимые категории</h4>
                  {stats.topCategories && stats.topCategories.length > 0 ? (
                    <div className="space-y-2">
                      {stats.topCategories.map((c, i) => (
                        <div key={i} className="flex justify-between items-center text-sm">
                          <span>{c.categoryName}</span>
                          <span className="text-muted-foreground">{c.visitCount} просмотров · {c.placesCount} мест</span>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <p className="text-sm text-muted-foreground">Просматривайте места — здесь появятся ваши любимые категории</p>
                  )}
                </div>
                {stats.topPlaceTypes && stats.topPlaceTypes.length > 0 && (
                  <div>
                    <h4 className="font-medium mb-2">Любимые типы отдыха</h4>
                    <div className="space-y-2">
                      {stats.topPlaceTypes.map((t, i) => (
                        <div key={i} className="flex justify-between items-center text-sm">
                          <span>{t.typeName}</span>
                          <span className="text-muted-foreground">{t.viewCount} просмотров · {t.placesCount} мест</span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
                {stats.activityByDate && stats.activityByDate.some((a) => a.views > 0) && (
                  <div>
                    <h4 className="font-medium mb-2 flex items-center gap-2">
                      <TrendingUp className="h-4 w-4" />
                      Активность за 30 дней
                    </h4>
                    <div className="h-48 w-full">
                      <ResponsiveContainer width="100%" height="100%">
                        <BarChart data={stats.activityByDate}>
                          <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
                          <XAxis dataKey="date" tick={{ fontSize: 10 }} tickFormatter={(v) => v ? new Date(v).toLocaleDateString("ru-RU", { day: "2-digit", month: "2-digit" }) : ""} />
                          <YAxis tick={{ fontSize: 10 }} />
                          <Tooltip formatter={(v) => [v ?? 0, "Просмотров"]} labelFormatter={(v) => (v ? new Date(v).toLocaleDateString("ru-RU") : "")} />
                          <Bar dataKey="views" fill="hsl(var(--primary))" radius={[4, 4, 0, 0]} />
                        </BarChart>
                      </ResponsiveContainer>
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>
          )}

          <Dialog open={createCollectionOpen} onOpenChange={setCreateCollectionOpen}>
            <DialogContent>
              <DialogHeader>
                <DialogTitle>Создать коллекцию</DialogTitle>
              </DialogHeader>
              <form onSubmit={handleCreateCollection} className="space-y-4">
                <div>
                  <Label>Название</Label>
                  <Input value={newCollectionName} onChange={(e) => setNewCollectionName(e.target.value)} placeholder="Мои планы на выходные" required />
                </div>
                <div>
                  <Label>Описание (необязательно)</Label>
                  <Input value={newCollectionDesc} onChange={(e) => setNewCollectionDesc(e.target.value)} placeholder="Описание коллекции" />
                </div>
                <DialogFooter>
                  <Button type="button" variant="outline" onClick={() => setCreateCollectionOpen(false)}>Отмена</Button>
                  <Button type="submit">Создать</Button>
                </DialogFooter>
              </form>
            </DialogContent>
          </Dialog>
        </div>
      </div>
    </Layout>
  );
};

export default Profile;
