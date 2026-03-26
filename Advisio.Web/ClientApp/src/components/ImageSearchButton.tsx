import { useState, useEffect } from "react";
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
  placeType?: string;
  placeAddress?: string;
}

function composeKeywordString(name: string, type?: string, address?: string): string {
  const parts = [name, type, address]
    .map((s) => (s || "").trim().replace(/\s+/g, " "))
    .filter(Boolean);
  const seen = new Set<string>();
  const tokens: string[] = [];
  for (const segment of parts) {
    for (const raw of segment.split(/\s+/)) {
      const w = raw.replace(/^[\p{P}\s]+|[\p{P}\s]+$/gu, "");
      if (w.length < 2) continue;
      const low = w.toLowerCase();
      if (seen.has(low)) continue;
      seen.add(low);
      tokens.push(w);
    }
  }
  return tokens.join(" ");
}

const ImageSearchButton = ({
  onSelect,
  placeName,
  placeType,
  placeAddress,
}: ImageSearchButtonProps) => {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<Array<{ url: string; thumbnailUrl?: string }>>([]);
  const [loading, setLoading] = useState(false);
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (open) {
      setQuery(composeKeywordString(placeName, placeType, placeAddress));
    }
  }, [open, placeName, placeType, placeAddress]);

  const search = async () => {
    const hasContext =
      placeName.trim().length > 0 ||
      !!placeType?.trim() ||
      !!placeAddress?.trim();
    if (!query.trim() && !hasContext) return;
    setLoading(true);
    try {
      const params = new URLSearchParams();
      params.set("query", query.trim());
      params.set("count", "10");
      if (placeName.trim()) params.set("placeName", placeName.trim());
      if (placeType?.trim()) params.set("placeType", placeType.trim());
      if (placeAddress?.trim()) params.set("placeAddress", placeAddress.trim());
      const res = await fetch(`/api/Images/search?${params.toString()}`);
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
        <Button type="button" variant="outline" size="icon" title="Поиск в Wikimedia Commons">
          <ImageIcon className="h-4 w-4" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-96 p-4" align="end">
        <p className="text-sm font-medium mb-1">Поиск изображения</p>
        <p className="text-xs text-muted-foreground mb-3">
          Подбор фото: стоки (если заданы ключи в настройках сервера), затем Wikimedia Commons; при отсутствии результатов — тестовые картинки (picsum).
        </p>
        <div className="flex gap-2 mb-3">
          <Input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Ключевые слова..."
            onKeyDown={(e) => e.key === "Enter" && search()}
          />
          <Button type="button" size="icon" onClick={search} disabled={loading}>
            <Search className="h-4 w-4" />
          </Button>
        </div>
        {loading && <p className="text-sm text-muted-foreground">Поиск...</p>}
        <div className="grid grid-cols-2 gap-2 max-h-64 overflow-y-auto">
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
