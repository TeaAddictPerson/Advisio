import { useEffect, useState } from "react";

/** Скрывает битый URL (403, истёкший CDN) и переключает на заглушку, как при отсутствии картинки. */
export function useResolvingImageUrl(url?: string | null) {
  const [broken, setBroken] = useState(false);
  useEffect(() => {
    setBroken(false);
  }, [url]);
  const effectiveUrl = url && !broken ? url : undefined;
  return { effectiveUrl, onImgError: () => setBroken(true) };
}
