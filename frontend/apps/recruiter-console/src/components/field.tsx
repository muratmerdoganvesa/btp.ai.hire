import type { InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from "react";
import { forwardRef } from "react";
import { cn } from "@hirelens/ui";

const fieldClass =
  "w-full rounded-2xl border border-border bg-white px-4 py-3.5 text-sm text-foreground outline-none transition-shadow placeholder:text-muted focus-visible:border-brand-5 focus-visible:ring-4 focus-visible:ring-brand-6/15";

export function Field({
  label,
  children
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <label className="flex flex-col gap-2">
      <span className="text-sm font-semibold text-foreground">{label}</span>
      {children}
    </label>
  );
}

export const TextInput = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(
  function TextInput({ className, ...props }, ref) {
    return <input ref={ref} className={cn(fieldClass, className)} {...props} />;
  }
);

export function TextArea({ className, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea className={cn(fieldClass, "min-h-32 resize-y leading-6", className)} {...props} />;
}

export function SelectInput({ className, ...props }: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select className={cn(fieldClass, className)} {...props} />;
}
