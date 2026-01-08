import { useState, useEffect, useMemo } from "react";
import Layout from "@/components/Layout";
import PlaceCard from "@/components/PlaceCard";
import PlaceFilters from "@/components/PlaceFilters";
import { Button } from "@/components/ui/button";
import { ChevronLeft, ChevronRight, MapPin } from "lucide-react";
import type { Place } from "@/components/PlaceCard";
import { toast } from "sonner";
import { useAuth } from "@/contexts/AuthContext";
import { isAdmin } from "@/lib/utils";
import EditPlaceDialog from "@/components/EditPlaceDialog";

const ITEMS_PER_PAGE = 6;

interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

const Places = () => {
  const { user, isLoading: authLoading } = useAuth();
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedCategory, setSelectedCategory] = useState("all");
  const [selectedType, setSelectedType] = useState("all");
  const [sortBy, setSortBy] = useState("default");
  const [currentPage, setCurrentPage] = useState(1);
  const [places, setPlaces] = useState<Place[]>([]);
  const [categories, setCategories] = useState<Array<{ id: number; name: string }>>([]);
  const [loading, setLoading] = useState(true);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [editDialogOpen, setEditDialogOpen] = useState(false);
  const [selectedPlace, setSelectedPlace] = useState<Place | null>(null);

  // Load categories
  useEffect(() => {
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
    loadCategories();
  }, []);

  // Load places
  useEffect(() => {
    const loadPlaces = async () => {
      // Не загружаем места, если категории еще не загружены и выбрана категория
      if (selectedCategory !== "all" && categories.length === 0) {
        setLoading(false);
        return;
      }
      setLoading(true);
      try {
        const params = new URLSearchParams();
        if (user) params.append("userId", user.id.toString());
        if (searchQuery) params.append("search", searchQuery);
        if (selectedCategory !== "all") {
          // Find category ID by name
          const cat = categories.find((c) => c.name === selectedCategory);
          if (cat) {
            params.append("categoryId", cat.id.toString());
          }
        }
        if (selectedType !== "all") params.append("type", selectedType);
        if (sortBy !== "default") params.append("sortBy", sortBy);
        params.append("page", currentPage.toString());
        params.append("pageSize", ITEMS_PER_PAGE.toString());

        const response = await fetch(`/api/Places?${params.toString()}`);
        if (response.ok) {
          const data: PagedResult<any> = await response.json();
          console.log("Places API response:", data);
          const placesData: Place[] = (data.items || []).map((p: any) => ({
            id: p.id,
            name: p.name || "Без названия",
            description: p.description || "",
            type: p.type || null,
            address: p.address || null,
            imageUrl: p.imageUrl || null,
            latitude: p.latitude || null,
            longitude: p.longitude || null,
            likes: p.likesCount || 0,
            categories: p.categories?.map((c: { name: string }) => c.name) || [],
            isLiked: p.isLiked || false,
            isFavorite: p.isFavorite || false,
          }));
          setPlaces(placesData);
          setTotalCount(data.totalCount);
          setTotalPages(data.totalPages);
        } else {
          const errorData = await response.json().catch(() => ({}));
          console.error("Failed to load places:", errorData);
          // Не показываем ошибку, если места просто не найдены (это нормально)
          if (response.status !== 404) {
            toast.error(errorData.error || "Не удалось загрузить места");
          }
          setPlaces([]);
          setTotalCount(0);
          setTotalPages(0);
        }
      } catch (error) {
        console.error("Failed to load places:", error);
        // Не показываем ошибку при обычных ошибках сети, только логируем
        setPlaces([]);
        setTotalCount(0);
        setTotalPages(0);
      } finally {
        setLoading(false);
      }
    };
    loadPlaces();
  }, [searchQuery, selectedCategory, selectedType, sortBy, currentPage, user, categories]);

  const handleLike = async (id: number) => {
    if (!user) {
      toast.error("Необходимо войти в систему");
      return;
    }
    try {
      const place = places.find((p) => p.id === id);
      const isLiked = place?.isLiked;

      const response = await fetch(`/api/Places/${id}/likes?userId=${user.id}`, {
        method: isLiked ? "DELETE" : "POST",
      });

      if (response.ok) {
        setPlaces((prev) =>
          prev.map((place) =>
            place.id === id
              ? {
                  ...place,
                  isLiked: !place.isLiked,
                  likes: place.isLiked ? place.likes - 1 : place.likes + 1,
                }
              : place
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
      const place = places.find((p) => p.id === id);
      const isFavorite = place?.isFavorite;

      const response = await fetch(`/api/Places/${id}/favorites?userId=${user.id}`, {
        method: isFavorite ? "DELETE" : "POST",
      });

      if (response.ok) {
        setPlaces((prev) =>
          prev.map((place) =>
            place.id === id ? { ...place, isFavorite: !place.isFavorite } : place
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

  const handleEdit = (id: number) => {
    const place = places.find((p) => p.id === id);
    if (place) {
      setSelectedPlace(place);
      setEditDialogOpen(true);
    }
  };

  const handlePlaceUpdated = () => {
    // Перезагружаем места после обновления
    const loadPlaces = async () => {
      if (selectedCategory !== "all" && categories.length === 0) {
        return;
      }

      try {
        const params = new URLSearchParams();
        if (user) params.append("userId", user.id.toString());
        if (searchQuery) params.append("search", searchQuery);
        if (selectedCategory !== "all") {
          const cat = categories.find((c) => c.name === selectedCategory);
          if (cat) {
            params.append("categoryId", cat.id.toString());
          }
        }
        if (selectedType !== "all") params.append("type", selectedType);
        if (sortBy !== "default") params.append("sortBy", sortBy);
        params.append("page", currentPage.toString());
        params.append("pageSize", ITEMS_PER_PAGE.toString());

        const response = await fetch(`/api/Places?${params.toString()}`);
        if (response.ok) {
          const data: PagedResult<any> = await response.json();
          const placesData: Place[] = (data.items || []).map((p: any) => ({
            id: p.id,
            name: p.name || "Без названия",
            description: p.description || "",
            type: p.type || null,
            address: p.address || null,
            imageUrl: p.imageUrl || null,
            latitude: p.latitude || null,
            longitude: p.longitude || null,
            likes: p.likesCount || 0,
            categories: p.categories?.map((c: { name: string }) => c.name) || [],
            isLiked: p.isLiked || false,
            isFavorite: p.isFavorite || false,
          }));
          setPlaces(placesData);
          setTotalCount(data.totalCount);
          setTotalPages(data.totalPages);
        }
      } catch (error) {
        console.error("Failed to reload places:", error);
      }
    };
    loadPlaces();
  };

  if (authLoading && loading) {
    return (
      <Layout>
        <div className="container py-16 text-center">
          <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-muted flex items-center justify-center">
            <MapPin className="h-8 w-8 text-muted-foreground animate-pulse" />
          </div>
          <p className="text-muted-foreground">Загрузка данных...</p>
        </div>
      </Layout>
    );
  }

  return (
    <Layout>
      <div className="container py-8 md:py-12">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <div>
            <h1 className="text-3xl md:text-4xl font-display font-bold text-foreground mb-2">
              Места отдыха
            </h1>
            <p className="text-muted-foreground">
              {loading ? "Загрузка..." : `Найдено ${totalCount} мест`}
            </p>
          </div>
        </div>

        {/* Filters */}
        <PlaceFilters
          searchQuery={searchQuery}
          onSearchChange={(query) => {
            setSearchQuery(query);
            setCurrentPage(1);
          }}
          selectedCategory={selectedCategory}
          onCategoryChange={(cat) => {
            setSelectedCategory(cat);
            setCurrentPage(1);
          }}
          selectedType={selectedType}
          onTypeChange={(type) => {
            setSelectedType(type);
            setCurrentPage(1);
          }}
          sortBy={sortBy}
          onSortChange={setSortBy}
          categories={categories.map(c => c.name)}
        />

        {/* Places Grid */}
        {loading ? (
          <div className="text-center py-16">
            <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-muted flex items-center justify-center">
              <MapPin className="h-8 w-8 text-muted-foreground animate-pulse" />
            </div>
            <p className="text-muted-foreground">Загрузка мест...</p>
          </div>
        ) : places.length > 0 ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {places.map((place, index) => (
              <div
                key={place.id}
                className="animate-slide-up opacity-0"
                style={{
                  animationDelay: `${index * 0.1}s`,
                  animationFillMode: "forwards",
                }}
              >
                <PlaceCard
                  place={place}
                  onLike={handleLike}
                  onFavorite={handleFavorite}
                  isAdmin={isAdmin(user?.role)}
                  onEdit={handleEdit}
                />
              </div>
            ))}
          </div>
        ) : (
          <div className="text-center py-16">
            <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-muted flex items-center justify-center">
              <MapPin className="h-8 w-8 text-muted-foreground" />
            </div>
            <h3 className="text-xl font-display font-semibold mb-2">
              Места не найдены
            </h3>
            <p className="text-muted-foreground">
              Попробуйте изменить параметры поиска
            </p>
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="mt-12 flex flex-col sm:flex-row items-center justify-between gap-4">
            <p className="text-sm text-muted-foreground order-2 sm:order-1">
              Показано {(currentPage - 1) * ITEMS_PER_PAGE + 1}-
              {Math.min(currentPage * ITEMS_PER_PAGE, totalCount)} из {totalCount} мест
            </p>
            <div className="flex items-center gap-2 order-1 sm:order-2">
              <Button
                variant="outline"
                size="icon"
                onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                disabled={currentPage === 1 || loading}
              >
                <ChevronLeft className="h-4 w-4" />
              </Button>
              {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                <Button
                  key={page}
                  variant={page === currentPage ? "default" : "outline"}
                  size="icon"
                  onClick={() => setCurrentPage(page)}
                  disabled={loading}
                >
                  {page}
                </Button>
              ))}
              <Button
                variant="outline"
                size="icon"
                onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                disabled={currentPage === totalPages || loading}
              >
                <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          </div>
        )}

        {/* Edit Place Dialog */}
        <EditPlaceDialog
          place={selectedPlace}
          open={editDialogOpen}
          onOpenChange={setEditDialogOpen}
          onSave={handlePlaceUpdated}
          categories={categories}
        />
      </div>
    </Layout>
  );
};

export default Places;
