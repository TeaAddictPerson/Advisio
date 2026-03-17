import { useState, useEffect, useRef } from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import Layout from "@/components/Layout";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { MapPin, ChevronRight, Lock, Globe, Pencil, ImagePlus, Trash2 } from "lucide-react";
import { toast } from "sonner";
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
  const navigate = useNavigate();
  const { user, isAuthenticated } = useAuth();
  const [collection, setCollection] = useState<{ id: number; name: string; description?: string; items: CollectionItem[] } | null>(null);
  const [loading, setLoading] = useState(true);
  const [editNoteOpen, setEditNoteOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<CollectionItem | null>(null);
  const [noteText, setNoteText] = useState("");
  const [notePhotoUrls, setNotePhotoUrls] = useState<string[]>([]);
  const [isNotePublic, setIsNotePublic] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editCollectionOpen, setEditCollectionOpen] = useState(false);
  const [editName, setEditName] = useState("");
  const [editDesc, setEditDesc] = useState("");
  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (id && user) {
      loadCollection(parseInt(id));
    }
  }, [id, user?.id]);

  const openEditNote = (item: CollectionItem) => {
    setEditingItem(item);
    setNoteText(item.note || "");
    setNotePhotoUrls(item.notePhotoUrls || []);
    setIsNotePublic(item.isNotePublic || false);
    setEditNoteOpen(true);
  };

  const handleSaveNote = async () => {
    if (!collection || !editingItem || !user) return;
    setSaving(true);
    try {
      const res = await fetch(`/api/Collections/${collection.id}/places/${editingItem.place.id}/note?userId=${user.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ note: noteText || null, notePhotoUrls, isNotePublic }),
      });
      if (res.ok) {
        toast.success("Заметка сохранена");
        setEditNoteOpen(false);
        loadCollection(collection.id);
      } else {
        const err = await res.json().catch(() => ({}));
        toast.error(err?.error || "Не удалось сохранить заметку");
      }
    } catch {
      toast.error("Ошибка при сохранении");
    } finally {
      setSaving(false);
    }
  };

  const handleAddPhoto = () => fileInputRef.current?.click();

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (!files?.length) return;
    for (const file of Array.from(files)) {
      if (!file.type.startsWith("image/")) {
        toast.error("Выберите изображение (jpg, png, gif...)");
        continue;
      }
      try {
        const formData = new FormData();
        formData.append("file", file);
        const res = await fetch("/api/Images/upload", { method: "POST", credentials: "include", body: formData });
        if (res.ok) {
          const data = await res.json();
          if (data.url) {
            setNotePhotoUrls((prev) => [...prev, data.url]);
            toast.success("Фото добавлено");
          }
        } else {
          const err = await res.json().catch(() => ({}));
          toast.error(err?.error || "Не удалось загрузить фото");
        }
      } catch {
        toast.error("Ошибка при загрузке");
      }
    }
    e.target.value = "";
  };

  const removePhotoUrl = (index: number) => setNotePhotoUrls((prev) => prev.filter((_, i) => i !== index));

  const openEditCollection = () => {
    setEditName(collection!.name);
    setEditDesc(collection!.description || "");
    setEditCollectionOpen(true);
  };

  const handleSaveCollection = async () => {
    if (!collection || !user || !editName.trim()) return;
    setSaving(true);
    try {
      const res = await fetch(`/api/Collections/${collection.id}?userId=${user.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ name: editName.trim(), description: editDesc.trim() || null }),
      });
      if (res.ok) {
        toast.success("Коллекция обновлена");
        setEditCollectionOpen(false);
        setCollection((c) => c ? { ...c, name: editName.trim(), description: editDesc.trim() || undefined } : null);
      } else {
        toast.error("Не удалось сохранить");
      }
    } catch {
      toast.error("Ошибка при сохранении");
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteCollection = async () => {
    if (!collection || !user) return;
    setSaving(true);
    try {
      const res = await fetch(`/api/Collections/${collection.id}?userId=${user.id}`, { method: "DELETE", credentials: "include" });
      if (res.ok) {
        toast.success("Коллекция удалена");
        navigate("/profile");
      } else {
        toast.error("Не удалось удалить");
      }
    } catch {
      toast.error("Ошибка при удалении");
    } finally {
      setSaving(false);
      setDeleteConfirmOpen(false);
    }
  };

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
          <CardHeader className="flex flex-row items-start justify-between gap-4">
            <div className="flex-1">
              <h1 className="text-2xl md:text-3xl font-display font-bold">{collection.name}</h1>
              {collection.description && (
                <p className="text-muted-foreground">{collection.description}</p>
              )}
            </div>
            <div className="flex gap-2 shrink-0">
              <Button variant="outline" size="sm" onClick={openEditCollection} className="gap-1">
                <Pencil className="h-4 w-4" /> Редактировать
              </Button>
              <Button variant="outline" size="sm" onClick={() => setDeleteConfirmOpen(true)} className="gap-1 text-destructive hover:text-destructive">
                <Trash2 className="h-4 w-4" /> Удалить
              </Button>
            </div>
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
                  <div className="flex justify-between items-start mb-2">
                    <Link to={`/place/${item.place.id}`}>
                      <h3 className="text-xl font-semibold hover:text-primary transition-colors">
                        {item.place.name}
                      </h3>
                    </Link>
                    <Button variant="ghost" size="sm" onClick={() => openEditNote(item)} className="gap-1">
                      <Pencil className="h-4 w-4" />
                      {item.note || item.notePhotoUrls?.length ? "Редактировать заметку" : "Добавить заметку"}
                    </Button>
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
                    <p className="text-sm text-muted-foreground italic">
                      Нет заметки. Нажмите «Заметка» чтобы добавить планы.
                    </p>
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

        <Dialog open={editNoteOpen} onOpenChange={setEditNoteOpen}>
          <DialogContent className="max-w-lg">
            <DialogHeader>
              <DialogTitle>Заметка о месте</DialogTitle>
            </DialogHeader>
            {editingItem && (
              <div className="space-y-4">
                <p className="text-sm text-muted-foreground">Место: {editingItem.place.name}</p>
                <div>
                  <Label>Текст заметки (планы, идеи)</Label>
                  <Textarea
                    value={noteText}
                    onChange={(e) => setNoteText(e.target.value)}
                    placeholder="Например: взять билеты заранее, прийти к 18:00..."
                    rows={4}
                    className="mt-1"
                  />
                </div>
                <div>
                  <Label className="flex items-center gap-2 mb-2">
                    <ImagePlus className="h-4 w-4" />
                    Фото с компьютера
                  </Label>
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept="image/*"
                    multiple
                    className="hidden"
                    onChange={handleFileSelect}
                  />
                  <div className="flex flex-wrap gap-2 mb-2">
                    {notePhotoUrls.map((url, i) => (
                      <div key={i} className="relative group">
                        <img src={url} alt="" className="w-20 h-20 object-cover rounded" />
                        <button
                          type="button"
                          onClick={() => removePhotoUrl(i)}
                          className="absolute -top-1 -right-1 w-5 h-5 rounded-full bg-destructive text-destructive-foreground text-xs"
                        >
                          ×
                        </button>
                      </div>
                    ))}
                    <button
                      type="button"
                      onClick={handleAddPhoto}
                      className="w-20 h-20 rounded border-2 border-dashed border-muted-foreground/30 flex items-center justify-center hover:border-primary/50"
                    >
                      <ImagePlus className="h-6 w-6 text-muted-foreground" />
                    </button>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <Switch id="public" checked={isNotePublic} onCheckedChange={setIsNotePublic} />
                  <Label htmlFor="public">
                    {isNotePublic ? "Публичная — видна в профиле" : "Приватная — только для вас"}
                  </Label>
                </div>
              </div>
            )}
            <DialogFooter>
              <Button variant="outline" onClick={() => setEditNoteOpen(false)}>Отмена</Button>
              <Button onClick={handleSaveNote} disabled={saving}>{saving ? "Сохранение..." : "Сохранить"}</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        <Dialog open={editCollectionOpen} onOpenChange={setEditCollectionOpen}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Редактировать коллекцию</DialogTitle>
            </DialogHeader>
            <div className="space-y-4">
              <div>
                <Label>Название</Label>
                <Input value={editName} onChange={(e) => setEditName(e.target.value)} placeholder="Название" required />
              </div>
              <div>
                <Label>Описание</Label>
                <Input value={editDesc} onChange={(e) => setEditDesc(e.target.value)} placeholder="Описание" />
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setEditCollectionOpen(false)}>Отмена</Button>
              <Button onClick={handleSaveCollection} disabled={saving || !editName.trim()}>{saving ? "Сохранение..." : "Сохранить"}</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        <AlertDialog open={deleteConfirmOpen} onOpenChange={setDeleteConfirmOpen}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Удалить коллекцию?</AlertDialogTitle>
              <AlertDialogDescription>
                Коллекция «{collection.name}» и все заметки будут удалены безвозвратно.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Отмена</AlertDialogCancel>
              <AlertDialogAction onClick={handleDeleteCollection} disabled={saving} className="bg-destructive text-destructive-foreground">
                {saving ? "Удаление..." : "Удалить"}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    </Layout>
  );
};

export default CollectionDetail;
