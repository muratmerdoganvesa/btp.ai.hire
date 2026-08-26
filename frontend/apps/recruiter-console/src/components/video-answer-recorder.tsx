import { Button } from "@hirelens/ui";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

type SpeechRecognitionLike = {
  lang: string;
  continuous: boolean;
  interimResults: boolean;
  onresult: ((event: { resultIndex: number; results: ArrayLike<{ 0: { transcript: string }; isFinal: boolean }> }) => void) | null;
  onerror: ((event: { error: string }) => void) | null;
  onend: (() => void) | null;
  start: () => void;
  stop: () => void;
};

function createRecognition(lang: string): SpeechRecognitionLike | null {
  const ctor =
    (window as unknown as { SpeechRecognition?: new () => SpeechRecognitionLike }).SpeechRecognition ??
    (window as unknown as { webkitSpeechRecognition?: new () => SpeechRecognitionLike }).webkitSpeechRecognition;
  if (!ctor) {
    return null;
  }
  const recognition = new ctor();
  recognition.lang = lang;
  recognition.continuous = true;
  recognition.interimResults = true;
  return recognition;
}

function captureJpegFromVideo(video: HTMLVideoElement | null, maxWidth = 640, quality = 0.72): string | null {
  if (!video || video.videoWidth < 2 || video.videoHeight < 2) {
    return null;
  }

  const scale = Math.min(1, maxWidth / video.videoWidth);
  const width = Math.max(1, Math.round(video.videoWidth * scale));
  const height = Math.max(1, Math.round(video.videoHeight * scale));
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext("2d");
  if (!ctx) {
    return null;
  }
  ctx.drawImage(video, 0, 0, width, height);
  return canvas.toDataURL("image/jpeg", quality);
}

export function VideoAnswerRecorder({
  question,
  questionIndex,
  questionTotal,
  disabled,
  onSubmit
}: {
  question: string;
  questionIndex: number;
  questionTotal: number;
  disabled?: boolean;
  onSubmit: (transcript: string, framesBase64: string[]) => Promise<void>;
}) {
  const { t, i18n } = useTranslation();
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const recorderRef = useRef<MediaRecorder | null>(null);
  const recognitionRef = useRef<SpeechRecognitionLike | null>(null);
  const chunksRef = useRef<Blob[]>([]);
  const framesRef = useRef<string[]>([]);

  const [cameraReady, setCameraReady] = useState(false);
  const [recording, setRecording] = useState(false);
  const [transcript, setTranscript] = useState("");
  const [interim, setInterim] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [sttSupported, setSttSupported] = useState(true);

  const pushFrame = () => {
    const frame = captureJpegFromVideo(videoRef.current);
    if (!frame) {
      return;
    }
    if (framesRef.current.length >= 3) {
      return;
    }
    framesRef.current = [...framesRef.current, frame];
  };

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const stream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: "user" },
          audio: true
        });
        if (cancelled) {
          stream.getTracks().forEach((track) => track.stop());
          return;
        }
        streamRef.current = stream;
        if (videoRef.current) {
          videoRef.current.srcObject = stream;
          await videoRef.current.play().catch(() => undefined);
        }
        setCameraReady(true);
        setSttSupported(Boolean(createRecognition(i18n.language === "tr" ? "tr-TR" : "en-US")));
      } catch {
        if (!cancelled) {
          setError(t("interview.cameraDenied"));
        }
      }
    })();

    return () => {
      cancelled = true;
      recognitionRef.current?.stop();
      recorderRef.current?.stop();
      streamRef.current?.getTracks().forEach((track) => track.stop());
      streamRef.current = null;
    };
  }, [i18n.language, t]);

  useEffect(() => {
    setTranscript("");
    setInterim("");
    setError(null);
    setRecording(false);
    framesRef.current = [];
  }, [question]);

  const startRecording = () => {
    setError(null);
    const stream = streamRef.current;
    if (!stream) {
      setError(t("interview.cameraDenied"));
      return;
    }

    framesRef.current = [];
    pushFrame();

    chunksRef.current = [];
    try {
      const mimeType = pickMimeType();
      const recorder = mimeType ? new MediaRecorder(stream, { mimeType }) : new MediaRecorder(stream);
      recorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          chunksRef.current.push(event.data);
        }
      };
      recorder.start(250);
      recorderRef.current = recorder;
    } catch {
      // Recording blob is optional; STT can still work.
    }

    const recognition = createRecognition(i18n.language === "tr" ? "tr-TR" : "en-US");
    if (recognition) {
      recognition.onresult = (event) => {
        let finalChunk = "";
        let interimChunk = "";
        for (let i = event.resultIndex; i < event.results.length; i++) {
          const result = event.results[i];
          if (result.isFinal) {
            finalChunk += result[0].transcript;
          } else {
            interimChunk += result[0].transcript;
          }
        }
        if (finalChunk) {
          setTranscript((prev) => `${prev} ${finalChunk}`.trim());
        }
        setInterim(interimChunk);
      };
      recognition.onerror = () => {
        setSttSupported(false);
      };
      recognition.onend = () => {
        if (recorderRef.current && recorderRef.current.state === "recording") {
          try {
            recognition.start();
          } catch {
            /* already stopped */
          }
        }
      };
      try {
        recognition.start();
        recognitionRef.current = recognition;
      } catch {
        setSttSupported(false);
      }
    } else {
      setSttSupported(false);
    }

    setRecording(true);
  };

  const stopRecording = () => {
    pushFrame();
    recognitionRef.current?.stop();
    recognitionRef.current = null;
    if (recorderRef.current && recorderRef.current.state !== "inactive") {
      recorderRef.current.stop();
    }
    recorderRef.current = null;
    setInterim("");
    setRecording(false);
  };

  const submit = async () => {
    const text = `${transcript} ${interim}`.trim();
    if (!text) {
      setError(t("interview.needTranscript"));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      pushFrame();
      stopRecording();
      const frames = framesRef.current.slice(0, 3);
      await onSubmit(text, frames);
      setTranscript("");
      setInterim("");
      framesRef.current = [];
    } catch {
      setError(t("errors.generic"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="rounded-xl border border-brand-2 bg-brand-0/50 px-4 py-3">
        <p className="text-xs font-bold uppercase tracking-[0.1em] text-brand-7">
          {t("interview.questionLabel", { current: questionIndex, total: questionTotal })}
        </p>
        <p className="mt-1 text-sm font-semibold leading-relaxed text-foreground">{question}</p>
      </div>

      <div className="overflow-hidden rounded-2xl border border-border bg-black">
        <video ref={videoRef} muted playsInline className="aspect-video w-full object-cover" />
      </div>

      {!sttSupported ? (
        <p className="text-xs text-muted">{t("interview.sttFallback")}</p>
      ) : null}

      <label className="flex flex-col gap-2 text-sm">
        <span className="font-semibold">{t("interview.transcriptLabel")}</span>
        <textarea
          className="min-h-28 w-full rounded-2xl border border-border bg-white px-4 py-3 text-sm outline-none focus-visible:border-brand-5 focus-visible:ring-4 focus-visible:ring-brand-6/15"
          value={`${transcript}${interim ? ` ${interim}` : ""}`.trim()}
          onChange={(event) => {
            setTranscript(event.target.value);
            setInterim("");
          }}
          placeholder={t("interview.transcriptPlaceholder")}
          disabled={disabled || submitting}
        />
      </label>

      {error ? (
        <p className="text-sm text-danger" role="alert">
          {error}
        </p>
      ) : null}

      <div className="flex flex-wrap gap-2">
        {!recording ? (
          <Button type="button" disabled={!cameraReady || disabled || submitting} onClick={startRecording}>
            {t("interview.recordStart")}
          </Button>
        ) : (
          <Button type="button" variant="outline" disabled={submitting} onClick={stopRecording}>
            {t("interview.recordStop")}
          </Button>
        )}
        <Button
          type="button"
          disabled={disabled || submitting || !`${transcript} ${interim}`.trim()}
          onClick={() => void submit()}
        >
          {submitting ? t("interview.submitting") : t("interview.submitAnswer")}
        </Button>
      </div>
    </div>
  );
}

function pickMimeType(): string {
  const candidates = ["video/webm;codecs=vp9,opus", "video/webm;codecs=vp8,opus", "video/webm"];
  return candidates.find((type) => MediaRecorder.isTypeSupported(type)) ?? "";
}
