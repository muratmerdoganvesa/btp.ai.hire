import { Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

type Frame = {
  id: string;
  contentType: string;
  imageBase64: string;
  capturedAt: string;
};

function toDataUrl(frame: Frame): string {
  if (frame.imageBase64.startsWith("data:")) {
    return frame.imageBase64;
  }
  return `data:${frame.contentType || "image/jpeg"};base64,${frame.imageBase64}`;
}

export function InterviewFramesGallery({ frames }: { frames: Frame[] }) {
  const { t } = useTranslation();
  if (!frames.length) {
    return null;
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("interview.photos")}</CardTitle>
        <p className="text-sm text-muted">{t("interview.photosHint")}</p>
      </CardHeader>
      <CardContent>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {frames.map((frame) => (
            <figure key={frame.id} className="overflow-hidden rounded-xl border border-border bg-brand-0/40">
              <img
                src={toDataUrl(frame)}
                alt={t("interview.photoAlt")}
                className="aspect-video w-full object-cover"
                loading="lazy"
              />
              <figcaption className="px-2 py-1.5 text-[11px] text-muted">
                {new Date(frame.capturedAt).toLocaleString()}
              </figcaption>
            </figure>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
