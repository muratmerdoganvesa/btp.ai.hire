import { create } from "zustand";

export type TourStepDef = {
  id: string;
  /** Path to open before highlighting. Use "first-position" for dynamic position. */
  route?: "/" | "/positions" | "first-position";
  target: string;
  titleKey: string;
  bodyKey: string;
};

export const TOUR_STEPS: TourStepDef[] = [
  {
    id: "funnel",
    route: "/",
    target: "tour-funnel",
    titleKey: "tour.stepFunnelTitle",
    bodyKey: "tour.stepFunnelBody"
  },
  {
    id: "recent",
    route: "/",
    target: "tour-recent",
    titleKey: "tour.stepRecentTitle",
    bodyKey: "tour.stepRecentBody"
  },
  {
    id: "oversight",
    route: "/",
    target: "tour-oversight",
    titleKey: "tour.stepOversightTitle",
    bodyKey: "tour.stepOversightBody"
  },
  {
    id: "nav-positions",
    route: "/",
    target: "tour-nav-positions",
    titleKey: "tour.stepNavTitle",
    bodyKey: "tour.stepNavBody"
  },
  {
    id: "composer",
    route: "/positions",
    target: "tour-composer",
    titleKey: "tour.stepComposerTitle",
    bodyKey: "tour.stepComposerBody"
  },
  {
    id: "list",
    route: "/positions",
    target: "tour-position-list",
    titleKey: "tour.stepListTitle",
    bodyKey: "tour.stepListBody"
  },
  {
    id: "candidates",
    route: "first-position",
    target: "tour-candidate-create",
    titleKey: "tour.stepCandidatesTitle",
    bodyKey: "tour.stepCandidatesBody"
  },
  {
    id: "upload",
    route: "first-position",
    target: "tour-cv-zone",
    titleKey: "tour.stepUploadTitle",
    bodyKey: "tour.stepUploadBody"
  },
  {
    id: "done",
    route: "/",
    target: "tour-funnel",
    titleKey: "tour.stepDoneTitle",
    bodyKey: "tour.stepDoneBody"
  }
];

type TourState = {
  active: boolean;
  index: number;
  autoPlay: boolean;
  start: (autoPlay?: boolean) => void;
  stop: () => void;
  next: () => void;
  prev: () => void;
  setIndex: (index: number) => void;
};

export const useTourStore = create<TourState>((set, get) => ({
  active: false,
  index: 0,
  autoPlay: true,
  start: (autoPlay = true) => set({ active: true, index: 0, autoPlay }),
  stop: () => set({ active: false, index: 0, autoPlay: false }),
  next: () => {
    const { index } = get();
    if (index >= TOUR_STEPS.length - 1) {
      set({ active: false, index: 0, autoPlay: false });
      return;
    }
    set({ index: index + 1 });
  },
  prev: () => set({ index: Math.max(0, get().index - 1) }),
  setIndex: (index) => set({ index })
}));
