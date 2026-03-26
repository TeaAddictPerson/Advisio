import { useState, useEffect, useRef } from "react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { toast } from "sonner";
import { Upload } from "lucide-react";
import type { Place } from "./PlaceCard";
import ImageSearchButton from "./ImageSearchButton";

interface EditPlaceDialogProps {
  place: Place | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: () => void;
  categories: Array<{ id: number; name: string }>;
  isAdmin?: boolean;
}

const PLACE_TYPES = [
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
];

const EditPlaceDialog = ({
  place,
  open,
  onOpenChange,
  onSave,
  categories,
  isAdmin = false,
}: EditPlaceDialogProps) => {
  const imageFileInputRef = useRef<HTMLInputElement>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [address, setAddress] = useState("");
  const [imageUrl, setImageUrl] = useState("");
  const [latitude, setLatitude] = useState("");
  const [longitude, setLongitude] = useState("");
  const [type, setType] = useState("");
  const [selectedCategories, setSelectedCategories] = useState<number[]>([]);
  const [loading, setLoading] = useState(false);
  const [uploadingImage, setUploadingImage] = useState(false);

  useEffect(() => {
    if (place) {
      setName(place.name || "");
      setDescription(place.description || "");
      setAddress(place.address || "");
      setImageUrl(place.imageUrl || "");
      setLatitude(place.latitude?.toString() || "");
      setLongitude(place.longitude?.toString() || "");
      setType(place.type || "");
      // Нормализуем categories: могут быть строки или объекты {id, name}
      const catNames = (place.categories || []).map((c) =>
        typeof c === "string" ? c : (c as { name?: string })?.name
      ).filter(Boolean) as string[];
      const categoryIds = catNames
        .map((catName) => categories.find((c) => c.name === catName)?.id)
        .filter((id): id is number => id !== undefined);
      setSelectedCategories(categoryIds);
    }
  }, [place, categories]);

  const handleSave = async () => {
    if (!place) return;

    if (!name.trim()) {
      toast.error("Название места обязательно");
      return;
    }

    setLoading(true);
    try {
      const response = await fetch(`/api/Places/${place.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: name.trim(),
          description: description.trim(),
          address: address.trim() || null,
          imageUrl: imageUrl.trim() || null,
          latitude: latitude ? parseFloat(latitude) : null,
          longitude: longitude ? parseFloat(longitude) : null,
          type: type || null,
          categoryIds: selectedCategories,
        }),
      });

      if (response.ok) {
        toast.success("Место обновлено");
        onSave();
        onOpenChange(false);
      } else {
        const error = await response.json();
        toast.error(error.error || "Не удалось обновить место");
      }
    } catch (error) {
      console.error("Failed to update place:", error);
      toast.error("Ошибка при обновлении места");
    } finally {
      setLoading(false);
    }
  };

  const toggleCategory = (categoryId: number) => {
    setSelectedCategories((prev) =>
      prev.includes(categoryId)
        ? prev.filter((id) => id !== categoryId)
        : [...prev, categoryId]
    );
  };

  const triggerImageUpload = () => imageFileInputRef.current?.click();

  const handleImageFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;
    if (!file.type.startsWith("image/")) {
      toast.error("Выберите изображение (jpg, png, gif, webp)…");
      return;
    }
    setUploadingImage(true);
    try {
      const formData = new FormData();
      formData.append("file", file);
      const res = await fetch("/api/Images/upload", {
        method: "POST",
        credentials: "include",
        body: formData,
      });
      if (res.ok) {
        const data = await res.json();
        if (data.url) {
          setImageUrl(data.url);
          toast.success("Изображение загружено");
        }
      } else {
        const err = await res.json().catch(() => ({}));
        toast.error(err?.error || "Не удалось загрузить файл");
      }
    } catch {
      toast.error("Ошибка при загрузке");
    } finally {
      setUploadingImage(false);
    }
  };

  // Не рендерим содержимое если place не загружен (избегаем падения)
  if (open && !place) {
    return (
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Редактировать место</DialogTitle>
          </DialogHeader>
          <p className="text-muted-foreground py-8 text-center">Загрузка...</p>
        </DialogContent>
      </Dialog>
    );
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Редактировать место</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="name">Название *</Label>
            <Input
              id="name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Название места"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">Описание *</Label>
            <Textarea
              id="description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Описание места"
              rows={4}
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="type">Тип</Label>
              <Select value={type || "__none__"} onValueChange={(v) => setType(v === "__none__" ? "" : v)}>
                <SelectTrigger>
                  <SelectValue placeholder="Выберите тип" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">Не указан</SelectItem>
                  {PLACE_TYPES.map((t) => (
                    <SelectItem key={t} value={t}>
                      {t}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="address">Адрес</Label>
              <Input
                id="address"
                value={address}
                onChange={(e) => setAddress(e.target.value)}
                placeholder="Адрес места"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="latitude">Широта</Label>
              <Input
                id="latitude"
                type="number"
                step="any"
                value={latitude}
                onChange={(e) => setLatitude(e.target.value)}
                placeholder="53.9045"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="longitude">Долгота</Label>
              <Input
                id="longitude"
                type="number"
                step="any"
                value={longitude}
                onChange={(e) => setLongitude(e.target.value)}
                placeholder="27.5615"
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="imageUrl">Изображение места</Label>
            <input
              ref={imageFileInputRef}
              type="file"
              accept="image/jpeg,image/png,image/gif,image/webp"
              className="hidden"
              onChange={handleImageFile}
            />
            <div className="flex flex-wrap gap-2 items-center">
              <Input
                id="imageUrl"
                className="flex-1 min-w-[12rem]"
                value={imageUrl}
                onChange={(e) => setImageUrl(e.target.value)}
                placeholder="URL или загрузите файл (админ)"
              />
              <ImageSearchButton
                onSelect={(url) => setImageUrl(url)}
                placeName={name}
                placeType={type}
                placeAddress={address}
              />
              {isAdmin && (
                <Button
                  type="button"
                  variant="outline"
                  size="icon"
                  title="Загрузить с компьютера"
                  disabled={uploadingImage}
                  onClick={triggerImageUpload}
                >
                  <Upload className="h-4 w-4" />
                </Button>
              )}
            </div>
          </div>

          <div className="space-y-2">
            <Label>Категории</Label>
            <div className="flex flex-wrap gap-2 p-3 border rounded-lg min-h-[60px]">
              {categories.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  Загрузка категорий...
                </p>
              ) : (
                categories.map((cat) => (
                  <Button
                    key={cat.id}
                    type="button"
                    variant={
                      selectedCategories.includes(cat.id)
                        ? "default"
                        : "outline"
                    }
                    size="sm"
                    onClick={() => toggleCategory(cat.id)}
                  >
                    {cat.name}
                  </Button>
                ))
              )}
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Отмена
          </Button>
          <Button onClick={handleSave} disabled={loading}>
            {loading ? "Сохранение..." : "Сохранить"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default EditPlaceDialog;

