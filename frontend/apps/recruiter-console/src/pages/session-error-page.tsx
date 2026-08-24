import { Button } from "@hirelens/ui";

export function SessionErrorPage() {
  const detail = typeof sessionStorage === "undefined" ? "" : (sessionStorage.getItem("hirelens.apiError") ?? "");

  return (
    <main className="flex min-h-screen items-center justify-center bg-background px-6">
      <div className="max-w-md text-center">
        <p className="text-sm font-medium text-brand">HireLens</p>
        <h1 className="mt-3 text-2xl font-semibold tracking-tight">Oturum API’ye bağlanamadı</h1>
        <p className="mt-3 text-sm leading-6 text-muted">
          SAP girişi tamamlandı. Recruiter API kullanıcı bilgisini kabul etmedi. Bu ekran yenileme döngüsünü durdurmak
          için gösterilir.
        </p>
        {detail ? <p className="mt-3 font-mono text-xs text-muted">{detail}</p> : null}
        <Button asChild className="mt-8">
          <a href="/logout">Çıkış yapıp tekrar dene</a>
        </Button>
      </div>
    </main>
  );
}
