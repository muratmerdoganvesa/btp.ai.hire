import { Button } from "@hirelens/ui";
import { useNavigate, useRouterState } from "@tanstack/react-router";
import { useEffect, useLayoutEffect, useRef, useState, type CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { TOUR_STEPS, useTourStore } from "./tour-store";

type Rect = { top: number; left: number; width: number; height: number };

function waitForTarget(id: string, timeoutMs = 8000): Promise<HTMLElement | null> {
  const started = Date.now();
  return new Promise((resolve) => {
    const tick = () => {
      const nodes = [...document.querySelectorAll<HTMLElement>(`[data-tour="${id}"]`)];
      const el =
        nodes.find((node) => {
          const style = window.getComputedStyle(node);
          const box = node.getBoundingClientRect();
          return style.display !== "none" && style.visibility !== "hidden" && box.width > 0 && box.height > 0;
        }) ?? nodes[0];
      if (el) {
        resolve(el);
        return;
      }
      if (Date.now() - started > timeoutMs) {
        resolve(null);
        return;
      }
      requestAnimationFrame(tick);
    };
    tick();
  });
}

export function ProductTour() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const pathname = useRouterState({ select: (s) => s.location.pathname });
  const { active, index, autoPlay, next, prev, stop } = useTourStore();
  const step = TOUR_STEPS[index];
  const [rect, setRect] = useState<Rect | null>(null);
  const [cursor, setCursor] = useState({ x: 48, y: 48 });
  const [ready, setReady] = useState(false);
  const navigating = useRef(false);

  useEffect(() => {
    if (!active || !step) {
      return;
    }

    let cancelled = false;
    setReady(false);
    setRect(null);

    const run = async () => {
      if (navigating.current) {
        return;
      }

      if (step.route === "/") {
        if (pathname !== "/") {
          navigating.current = true;
          await navigate({ to: "/" });
          navigating.current = false;
          if (cancelled) {
            return;
          }
        }
      } else if (step.route === "/positions") {
        if (pathname !== "/positions") {
          navigating.current = true;
          await navigate({ to: "/positions" });
          navigating.current = false;
          if (cancelled) {
            return;
          }
        }
      } else if (step.route === "/positions/new") {
        if (pathname !== "/positions/new") {
          navigating.current = true;
          await navigate({ to: "/positions/new" });
          navigating.current = false;
          if (cancelled) {
            return;
          }
        }
      } else if (step.route === "first-position") {
        const onPositionDetail = /^\/positions\/[^/]+$/.test(pathname);
        if (!onPositionDetail) {
          navigating.current = true;
          try {
            const positions = await api.listPositions();
            const first = positions[0];
            if (first) {
              await navigate({ to: "/positions/$positionId", params: { positionId: first.id } });
            } else {
              await navigate({ to: "/positions" });
            }
          } finally {
            navigating.current = false;
          }
          if (cancelled) {
            return;
          }
        }
      }

      const el = await waitForTarget(step.target);
      if (cancelled || !el) {
        setReady(true);
        return;
      }

      el.scrollIntoView({ behavior: "smooth", block: "center", inline: "nearest" });
      await new Promise((r) => setTimeout(r, 350));
      if (cancelled) {
        return;
      }

      const box = el.getBoundingClientRect();
      const nextRect = {
        top: box.top,
        left: box.left,
        width: box.width,
        height: box.height
      };
      setRect(nextRect);
      setCursor({
        x: nextRect.left + Math.min(nextRect.width * 0.55, nextRect.width - 12),
        y: nextRect.top + Math.min(nextRect.height * 0.45, nextRect.height - 12)
      });
      setReady(true);
    };

    void run();
    return () => {
      cancelled = true;
    };
  }, [active, index, step, pathname, navigate]);

  useLayoutEffect(() => {
    if (!active || !rect) {
      return;
    }
    const onResize = () => {
      const el = document.querySelector<HTMLElement>(`[data-tour="${step.target}"]`);
      if (!el) {
        return;
      }
      const box = el.getBoundingClientRect();
      setRect({ top: box.top, left: box.left, width: box.width, height: box.height });
    };
    window.addEventListener("resize", onResize);
    window.addEventListener("scroll", onResize, true);
    return () => {
      window.removeEventListener("resize", onResize);
      window.removeEventListener("scroll", onResize, true);
    };
  }, [active, rect, step.target]);

  useEffect(() => {
    if (!active || !autoPlay || !ready) {
      return;
    }
    const timer = window.setTimeout(() => next(), 4200);
    return () => window.clearTimeout(timer);
  }, [active, autoPlay, ready, index, next]);

  if (!active || !step) {
    return null;
  }

  const pad = 10;
  const hole = rect
    ? {
        top: Math.max(8, rect.top - pad),
        left: Math.max(8, rect.left - pad),
        width: rect.width + pad * 2,
        height: rect.height + pad * 2
      }
    : null;

  const cardStyle: CSSProperties = hole
    ? {
        top: Math.min(window.innerHeight - 220, hole.top + hole.height + 16),
        left: Math.min(window.innerWidth - 360, Math.max(16, hole.left))
      }
    : { top: "30%", left: "50%", transform: "translateX(-50%)" };

  return (
    <div className="pointer-events-none fixed inset-0 z-[80]" aria-live="polite">
      <div className="absolute inset-0 bg-foreground/45" />
      {hole ? (
        <div
          className="tour-spotlight absolute rounded-xl border-2 border-brand bg-transparent shadow-[0_0_0_9999px_rgba(15,23,32,0.55)] transition-all duration-500 ease-out"
          style={{
            top: hole.top,
            left: hole.left,
            width: hole.width,
            height: hole.height
          }}
        />
      ) : null}

      <div
        className="tour-cursor absolute z-[90] transition-[left,top] duration-700 ease-[cubic-bezier(0.22,1,0.36,1)]"
        style={{ left: cursor.x, top: cursor.y }}
        aria-hidden="true"
      >
        <svg width="28" height="28" viewBox="0 0 24 24" fill="none">
          <path
            d="M5 3.5 19 12l-7.2 1.6L9.5 21 5 3.5Z"
            fill="var(--hl-brand-7, #0f4c5c)"
            stroke="white"
            strokeWidth="1.2"
          />
        </svg>
      </div>

      <div
        className="pointer-events-auto absolute z-[95] w-[min(22rem,calc(100vw-2rem))] rounded-2xl border border-border bg-surface p-5 shadow-card"
        style={cardStyle}
        role="dialog"
        aria-labelledby="tour-title"
      >
        <p className="text-[0.7rem] font-semibold uppercase tracking-[0.16em] text-brand">
          {t("tour.badge", { current: index + 1, total: TOUR_STEPS.length })}
        </p>
        <h2 id="tour-title" className="font-display mt-2 text-xl font-semibold tracking-tight">
          {t(step.titleKey)}
        </h2>
        <p className="mt-2 text-sm leading-6 text-muted">{t(step.bodyKey)}</p>
        <div className="mt-4 flex flex-wrap items-center gap-2">
          <Button type="button" size="sm" variant="outline" onClick={stop}>
            {t("tour.skip")}
          </Button>
          <Button type="button" size="sm" variant="outline" disabled={index === 0} onClick={prev}>
            {t("tour.back")}
          </Button>
          <Button type="button" size="sm" onClick={next}>
            {index >= TOUR_STEPS.length - 1 ? t("tour.finish") : t("tour.next")}
          </Button>
        </div>
        {autoPlay ? <p className="mt-3 text-xs text-muted">{t("tour.autoHint")}</p> : null}
      </div>
    </div>
  );
}
