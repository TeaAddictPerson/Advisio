import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { Input } from "@/components/ui/input";
import { Search, ImageIcon } from "lucide-react";

interface ImageSearchButtonProps {
  onSelect: (url: string) => void;
  placeName: string;
}

const ImageSearchButton = ({ onSelect, placeName }: ImageSearchButtonProps) => {
  const [query, setQuery] = useState(placeName || "");
  const [results, setResults] = useState<Array<{ url: string; thumbnailUrl?: string }>>([]);
  const [loading, setLoading] = useState(false);
  const [open, setOpen] = useState(false);

  const search = async () => {
    if (!query.trim()) return;
    setLoading(true);
    try {
      const res = await fetch(`/api/Images/search?query=${encodeURIComponent(query)}&count=6`);
      if (res.ok) {
        const data = await res.json();
        setResults(data);
      }
    } catch (e) {
      console.error("Image search failed:", e);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button type="button" variant="outline" size="icon" title="Поиск изображения">
          <ImageIcon className="h-4 w-4" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-80 p-4" align="end">
        <p className="text-sm font-medium mb-2">Поиск изображения</p>
        <div className="flex gap-2 mb-3">
          <Input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Название места..."
            onKeyDown={(e) => e.key === "Enter" && search()}
          />
          <Button type="button" size="icon" onClick={search} disabled={loading}>
            <Search className="h-4 w-4" />
          </Button>
        </div>
        {loading && <p className="text-sm text-muted-foreground">Поиск...</p>}
        <div className="grid grid-cols-2 gap-2 max-h-48 overflow-y-auto">
          {results.map((img, i) => (
            <button
              key={i}
              type="button"
              className="aspect-video rounded overflow-hidden border hover:border-primary"
              onClick={() => {
                onSelect(img.url);
                setOpen(false);
              }}
            >
              <img src={img.thumbnailUrl || img.url} alt="" className="w-full h-full object-cover" />
            </button>
          ))}
        </div>
      </PopoverContent>
    </Popover>
  );
};

export default ImageSearchButton;
