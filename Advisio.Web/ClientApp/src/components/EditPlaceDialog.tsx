import { useState, useEffect } from "react";
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
import type { Place } from "./PlaceCard";

interface EditPlaceDialogProps {
  place: Place | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: () => void;
  categories: Array<{ id: number; name: string }>;
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
}: EditPlaceDialogProps) => {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [address, setAddress] = useState("");
  const [imageUrl, setImageUrl] = useState("");
  const [latitude, setLatitude] = useState("");
  const [longitude, setLongitude] = useState("");
  const [type, setType] = useState("");
  const [selectedCategories, setSelectedCategories] = useState<number[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (place) {
      setName(place.name || "");
      setDescription(place.description || "");
      setAddress(place.address || "");
      setImageUrl(place.imageUrl || "");
      setLatitude(place.latitude?.toString() || "");
      setLongitude(place.longitude?.toString() || "");
      setType(place.type || "");
      // Получаем ID категорий по именам
      const categoryIds = place.categories
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
              <Select value={type} onValueChange={setType}>
                <SelectTrigger>
                  <SelectValue placeholder="Выберите тип" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="">Не указан</SelectItem>
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
            <Label htmlFor="imageUrl">URL изображения</Label>
            <Input
              id="imageUrl"
              value={imageUrl}
              onChange={(e) => setImageUrl(e.target.value)}
              placeholder="https://example.com/image.jpg"
            />
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

