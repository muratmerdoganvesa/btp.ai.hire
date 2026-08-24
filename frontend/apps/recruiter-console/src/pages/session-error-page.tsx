import { Button } from "@hirelens/ui";

export function SessionErrorPage() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-background px-6">
      <div className="max-w-md text-center">
        <p className="text-sm font-medium text-brand">HireLens</p>
        <h1 className="mt-3 text-2xl font-semibold tracking-tight">Oturum API’ye bağlanamadı</h1>
        <p className="mt-3 text-sm leading-6 text-muted">
          SAP girişi tamamlandı. Recruiter arayüzü API’den kullanıcı bilgisini alamadığı için sayfa yenilenmiyor;
          aksi halde tarayıcı sonsuz döngüye girer.
        </p>
        <Button asChild className="mt-8">
          <a href="/logout">Çıkış yapıp tekrar dene</a>
        </Button>
      </div>
    </main>
  );
}
