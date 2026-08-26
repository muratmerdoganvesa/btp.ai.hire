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

function createMediaRecorder(stream: MediaStream): MediaRecorder | null {
  const mimeCandidates = [
    "video/webm;codecs=vp8,opus",
    "video/webm;codecs=vp9,opus",
    "video/webm;codecs=vp8",
    "video/webm",
    ""
  ];
  for (const mimeType of mimeCandidates) {
    if (mimeType && typeof MediaRecorder !== "undefined" && !MediaRecorder.isTypeSupported(mimeType)) {
      continue;
    }
    try {
      return mimeType ? new MediaRecorder(stream, { mimeType }) : new MediaRecorder(stream);
    } catch {
      /* try next */
    }
  }
  return null;
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
  /** Chrome STT ends often; restart while this is true (do not depend on MediaRecorder). */
  const liveRef = useRef(false);
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
          video: { facingMode: "user", width: { ideal: 1280 }, height: { ideal: 720 } },
          audio: {
            echoCancellation: true,
            noiseSuppression: true,
            autoGainControl: true
          }
        });
        if (cancelled) {
          stream.getTracks().forEach((track) => track.stop());
          return;
        }
        streamRef.current = stream;
        const video = videoRef.current;
        if (video) {
          video.srcObject = stream;
          video.muted = true;
          video.playsInline = true;
          await video.play().catch(() => undefined);
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
      liveRef.current = false;
      recognitionRef.current?.stop();
      recognitionRef.current = null;
      if (recorderRef.current && recorderRef.current.state !== "inactive") {
        try {
          recorderRef.current.stop();
        } catch {
          /* ignore */
        }
      }
      recorderRef.current = null;
      streamRef.current?.getTracks().forEach((track) => track.stop());
      streamRef.current = null;
    };
  }, [i18n.language, t]);

  useEffect(() => {
    liveRef.current = false;
    recognitionRef.current?.stop();
    recognitionRef.current = null;
    setTranscript("");
    setInterim("");
    setError(null);
    setRecording(false);
    framesRef.current = [];
  }, [question]);

  const startRecognition = () => {
    recognitionRef.current?.stop();
    recognitionRef.current = null;

    const recognition = createRecognition(i18n.language === "tr" ? "tr-TR" : "en-US");
    if (!recognition) {
      setSttSupported(false);
      return;
    }

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
    recognition.onerror = (event) => {
      // Chrome fires recoverable errors; only hard-fail on permission/service.
      if (event.error === "not-allowed" || event.error === "service-not-allowed") {
        setSttSupported(false);
        liveRef.current = false;
        setRecording(false);
        setError(t("interview.micDenied"));
        return;
      }
      if (event.error === "network") {
        setSttSupported(false);
      }
    };
    recognition.onend = () => {
      if (!liveRef.current) {
        return;
      }
      // Chrome ends continuous sessions; restart while user is still "recording".
      window.setTimeout(() => {
        if (!liveRef.current || recognitionRef.current !== recognition) {
          return;
        }
        try {
          recognition.start();
        } catch {
          try {
            startRecognition();
          } catch {
            /* give up */
          }
        }
      }, 120);
    };

    try {
      recognition.start();
      recognitionRef.current = recognition;
      setSttSupported(true);
    } catch {
      setSttSupported(false);
    }
  };

  const startRecording = () => {
    setError(null);
    const stream = streamRef.current;
    if (!stream) {
      setError(t("interview.cameraDenied"));
      return;
    }

    // Ensure preview is playing (Chrome sometimes pauses after permission).
    void videoRef.current?.play().catch(() => undefined);

    framesRef.current = [];
    pushFrame();
    chunksRef.current = [];

    const recorder = createMediaRecorder(stream);
    if (recorder) {
      recorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          chunksRef.current.push(event.data);
        }
      };
      try {
        recorder.start(1000);
        recorderRef.current = recorder;
      } catch {
        recorderRef.current = null;
      }
    } else {
      recorderRef.current = null;
    }

    liveRef.current = true;
    startRecognition();
    setRecording(true);
  };

  const stopRecording = () => {
    pushFrame();
    liveRef.current = false;
    recognitionRef.current?.stop();
    recognitionRef.current = null;
    if (recorderRef.current && recorderRef.current.state !== "inactive") {
      try {
        recorderRef.current.stop();
      } catch {
        /* ignore */
      }
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

      <div className="relative overflow-hidden rounded-2xl border border-border bg-black">
        <video ref={videoRef} muted playsInline autoPlay className="aspect-video w-full object-cover" />
        {recording ? (
          <div className="absolute left-3 top-3 flex items-center gap-2 rounded-md bg-black/65 px-2.5 py-1 text-xs font-semibold text-white">
            <span className="inline-block h-2 w-2 animate-pulse rounded-full bg-red-500" aria-hidden />
            {t("interview.recordingLive")}
          </div>
        ) : null}
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
