import { useState, useEffect } from "react";
import { Link, useNavigate } from "react-router-dom";
import Layout from "@/components/Layout";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Eye, EyeOff, User, Heart, Star, LogOut } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { toast } from "sonner";
import { isAdmin } from "@/lib/utils";
import PlaceCard from "@/components/PlaceCard";
import type { Place } from "@/components/PlaceCard";

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

  useEffect(() => {
    if (isAuthenticated && user) {
      setUsername(user.username);
      setEmail(user.email);
      setCurrentPassword("");
      setNewPassword("");
      setFavorites([]);
      setLiked([]);
      loadUserPlaces();
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
    <Layout 
      isAuthenticated={isAuthenticated} 
      username={user?.username} 
      isAdmin={isAdmin(user?.role)}
      onLogout={handleLogout}
    >
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
        </div>
      </div>
    </Layout>
  );
};

export default Profile;
