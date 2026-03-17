import { useState, useEffect } from "react";
import { useParams, Link } from "react-router-dom";
import Layout from "@/components/Layout";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { MapPin, ChevronRight, Lock, Globe } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import type { Place } from "@/components/PlaceCard";

interface CollectionItem {
  place: Place;
  note?: string;
  notePhotoUrls: string[];
  isNotePublic: boolean;
}

const CollectionDetail = () => {
  const { id } = useParams<{ id: string }>();
  const { user, isAuthenticated } = useAuth();
  const [collection, setCollection] = useState<{ id: number; name: string; description?: string; items: CollectionItem[] } | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (id && user) {
      loadCollection(parseInt(id));
    }
  }, [id, user?.id]);

  const loadCollection = async (collectionId: number) => {
    setLoading(true);
    try {
      const res = await fetch(`/api/Collections/${collectionId}?userId=${user!.id}`);
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
          categories: (p.categories || []).map((c: { name?: string } | string) => typeof c === "string" ? c : c?.name || ""),
          isLiked: p.isLiked || false,
          isFavorite: p.isFavorite || false,
        });
        const rawItems = data.items || [];
        const items: CollectionItem[] = rawItems.length > 0
          ? rawItems.map((item: any) => ({
              place: mapPlace(item.place || item),
              note: item.note,
              notePhotoUrls: item.notePhotoUrls || [],
              isNotePublic: item.isNotePublic || false,
            }))
          : (data.places || []).map((p: any) => ({
              place: mapPlace(p),
              note: undefined,
              notePhotoUrls: [],
              isNotePublic: false,
            }));
        setCollection({
          id: data.id,
          name: data.name,
          description: data.description,
          items,
        });
      } else {
        setCollection(null);
      }
    } catch (e) {
      console.error("Failed to load collection:", e);
      setCollection(null);
    } finally {
      setLoading(false);
    }
  };

  if (!isAuthenticated || !user) {
    return (
      <Layout>
        <div className="container py-16 text-center">
          <p className="text-muted-foreground">Войдите в систему для просмотра коллекций</p>
          <Link to="/login">
            <Button className="mt-4">Войти</Button>
          </Link>
        </div>
      </Layout>
    );
  }

  if (loading) {
    return (
      <Layout>
        <div className="container py-16 text-center">
          <p className="text-muted-foreground">Загрузка...</p>
        </div>
      </Layout>
    );
  }

  if (!collection) {
    return (
      <Layout>
        <div className="container py-16 text-center">
          <h1 className="text-2xl font-display font-bold mb-4">Коллекция не найдена</h1>
          <Link to="/profile">
            <Button>В профиль</Button>
          </Link>
        </div>
      </Layout>
    );
  }

  return (
    <Layout>
      <div className="container py-8 md:py-12">
        <nav className="flex items-center gap-2 text-sm text-muted-foreground mb-6">
          <Link to="/profile" className="hover:text-foreground transition-colors">Профиль</Link>
          <ChevronRight className="h-4 w-4" />
          <span className="text-foreground font-medium">{collection.name}</span>
        </nav>

        <Card variant="elevated" className="mb-6">
          <CardHeader>
            <h1 className="text-2xl md:text-3xl font-display font-bold">{collection.name}</h1>
            {collection.description && (
              <p className="text-muted-foreground">{collection.description}</p>
            )}
          </CardHeader>
        </Card>

        <div className="space-y-6">
          {collection.items.map((item) => (
            <Card key={item.place.id} variant="default" className="overflow-hidden">
              <div className="flex flex-col md:flex-row">
                <div className="md:w-64 flex-shrink-0">
                  {item.place.imageUrl ? (
                    <img
                      src={item.place.imageUrl}
                      alt={item.place.name}
                      className="w-full h-48 md:h-full object-cover"
                    />
                  ) : (
                    <div className="w-full h-48 md:h-48 bg-muted flex items-center justify-center">
                      <MapPin className="h-12 w-12 text-muted-foreground/50" />
                    </div>
                  )}
                </div>
                <CardContent className="flex-1 p-6">
                  <div className="mb-2">
                    <Link to={`/place/${item.place.id}`}>
                      <h3 className="text-xl font-semibold hover:text-primary transition-colors">
                        {item.place.name}
                      </h3>
                    </Link>
                  </div>
                  <p className="text-sm text-muted-foreground mb-3 line-clamp-2">
                    {item.place.description?.substring(0, 150)}...
                  </p>
                  {(item.note || item.notePhotoUrls?.length) ? (
                    <div className="p-3 rounded-lg bg-muted/50 border border-border">
                      {item.isNotePublic ? (
                        <span className="text-xs text-muted-foreground flex items-center gap-1 mb-1">
                          <Globe className="h-3 w-3" /> Публичная заметка
                        </span>
                      ) : (
                        <span className="text-xs text-muted-foreground flex items-center gap-1 mb-1">
                          <Lock className="h-3 w-3" /> Приватная заметка
                        </span>
                      )}
                      {item.note && <p className="text-sm whitespace-pre-line">{item.note}</p>}
                      {item.notePhotoUrls?.length > 0 && (
                        <div className="flex gap-2 mt-2 flex-wrap">
                          {item.notePhotoUrls.map((url, i) => (
                            <img key={i} src={url} alt="" className="w-16 h-16 object-cover rounded" />
                          ))}
                        </div>
                      )}
                    </div>
                  ) : (
                    <p className="text-sm text-muted-foreground italic">Нет заметки</p>
                  )}
                  <Link to={`/place/${item.place.id}`} className="inline-block mt-3 text-sm text-primary">
                    Подробнее о месте →
                  </Link>
                </CardContent>
              </div>
            </Card>
          ))}
        </div>

        {collection.items.length === 0 && (
          <div className="text-center py-12">
            <p className="text-muted-foreground mb-4">В коллекции пока нет мест</p>
            <Link to="/places">
              <Button>Добавить места</Button>
            </Link>
          </div>
        )}
      </div>
    </Layout>
  );
};

export default CollectionDetail;
