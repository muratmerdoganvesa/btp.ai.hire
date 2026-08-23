namespace HireLens.Infrastructure.Seed;

public sealed record DemoSeedPosition(
    string Department,
    string Title,
    string JobDescription,
    IReadOnlyList<(string Name, string Description, int Weight)> Criteria);

public sealed record DemoSeedCv(
    string Department,
    string PositionTitle,
    string CandidateName,
    string FileName,
    string Text);

public static class DemoCvCatalog
{
    public const int ExpectedCount = 500;
    public const string TitlePrefix = "SEED · ";

    private static readonly string[] FirstNames =
    [
        "Ada", "Baran", "Ceren", "Deniz", "Ekin", "Ferhat", "Gökçe", "Hakan", "İpek", "Kaan",
        "Leyla", "Mert", "Nisan", "Ozan", "Pınar", "Rüzgar", "Selin", "Tuna", "Umut", "Yasemin",
        "Ali", "Buse", "Can", "Derya", "Emre", "Fulya", "Gizem", "Halil", "İrem", "Kerem"
    ];

    private static readonly string[] LastNames =
    [
        "Yılmaz", "Kaya", "Demir", "Çelik", "Şahin", "Aydın", "Öztürk", "Arslan", "Doğan", "Kurt",
        "Koç", "Özdemir", "Şimşek", "Polat", "Acar", "Yıldız", "Kılıç", "Çetin", "Erdoğan", "Aslan"
    ];

    private static readonly string[] Cities =
    [
        "İstanbul", "Ankara", "İzmir", "Bursa", "Eskişehir", "Kocaeli", "Antalya", "Gaziantep"
    ];

    private static readonly string[] Levels = ["Stajyer", "Junior", "Orta", "Kıdemli", "Lead"];

    public static IReadOnlyList<DemoSeedPosition> Positions { get; } = BuildPositions();

    public static IReadOnlyList<DemoSeedCv> Cvs { get; } = BuildCvs();

    private static IReadOnlyList<DemoSeedPosition> BuildPositions() =>
    [
        Pos("Yazılım Mühendisliği", "Backend / SAP BTP geliştirici",
            "2026 ürün hattı için .NET 10 ve SAP BTP üzerinde kanıta bağlı servisler. CAP, HANA Cloud, XSUAA ve AI Core ile çalışan backend.",
            ("C# / .NET 10", "Minimal API, C# 14, test", 40),
            ("SAP BTP", "CF Docker, XSUAA, HANA", 35),
            ("SQL / HANA", "Sorgu, şema, çok kiracılı veri", 25)),
        Pos("Veri ve Analitik", "Analitik mühendis",
            "İşe alım hunisi ve bias bantları için HANA + semantik modeller. Sayı, kanıt olmadan yayınlanmaz.",
            ("SQL / HANA", "Modelleme ve performans", 40),
            ("Python", "ETL, kalite, notebook", 35),
            ("Görselleştirme", "Funnel, cohort, Recharts/SAC", 25)),
        Pos("Bulut ve DevOps", "BTP / cloud mühendisi",
            "CF Docker, GHCR, observability. Aylık base image yenileme ve sıfır downtime hedefi.",
            ("CI/CD", "GitHub Actions, cf push", 40),
            ("SAP BTP", "Destinations, autoscaler, logs", 35),
            ("Gözlemlenebilirlik", "OpenTelemetry, Serilog", 25)),
        Pos("Siber Güvenlik", "Uygulama güvenlik uzmanı",
            "JWT-only tenant, secret yalnız VCAP, PII maskeleme. Yüksek riskli istihdam değerlendirmesi.",
            ("AppSec", "OWASP, JWT, tehdit modeli", 40),
            ("BTP güvenlik", "XSUAA, IAS, Audit Log", 35),
            ("KVKK", "Silme, export, audit", 25)),
        Pos("Ürün Yönetimi", "Ürün yöneticisi",
            "Kanıta bağlı skor, otomatik kabul/red yok. Discovery’den BTP teslimine.",
            ("Keşif", "Problem, metrik, görüşme", 40),
            ("Teslim", "Yol haritası, kesit, ADR", 35),
            ("Paydaş", "İK, hukuk, mimari", 25)),
        Pos("UX ve Tasarım", "Ürün tasarımcısı",
            "Token-only arayüz, aday fotoğrafı yok, düşük skor gri. WCAG 2.2 AA.",
            ("Ürün tasarımı", "Akış, empty/error", 40),
            ("Erişilebilirlik", "WCAG 2.2 AA", 35),
            ("Tasarım sistemi", "Token, OKLCH, shadcn", 25)),
        Pos("Satış", "Kurumsal satış",
            "Çok kiracılı HireLens’i holding İK’ya satmak. Kanıt ve KVKK vaadi.",
            ("Kurumsal satış", "C-level, RFP", 45),
            ("İK alanı", "ATS, SF, işe alım", 30),
            ("BTP / SAP", "Mevcut SAP hesabı", 25)),
        Pos("Pazarlama", "Ürün pazarlama",
            "Kanıta bağlı işe alım anlatısı. AB AI Act ve KVKK iddiası abartılmaz.",
            ("Ürün pazarlama", "Mesaj, lansman", 40),
            ("İçerik", "TR/EN, vaka, webinar", 35),
            ("Talep", "Kampanya, ölçüm", 25)),
        Pos("Müşteri Başarısı", "Müşteri başarı yöneticisi",
            "Go-live, rol koleksiyonu, HANA şema, değer kanıtı.",
            ("CSM", "Onboarding, QBR", 40),
            ("İK süreç", "Requisition, mülakat", 35),
            ("BTP", "Subaccount, destinasyon", 25)),
        Pos("İnsan Kaynakları", "İşe alım uzmanı",
            "Kanıt okuyan recruiter. Otomatik eleme yok; gerekçe insanda.",
            ("İşe alım", "Sourcing, mülakat", 45),
            ("Değerlendirme", "Rubrik, kanıt", 30),
            ("KVKK", "Aday hakları", 25)),
        Pos("Finans", "Finansal kontrolör",
            "Metering, kota, faturalama. Token kullanımı ve sözleşme.",
            ("Kontrol", "Bütçe, forecast", 40),
            ("SaaS finans", "Kota, birim ekonomi", 35),
            ("Rapor", "HANA / Excel modelleri", 25)),
        Pos("Hukuk ve Uyum", "KVKK / AI Act danışmanı",
            "İstihdam değerlendirmesi yüksek risk. Açıklanabilirlik ve silme.",
            ("KVKK / GDPR", "Hukuki dayanak, DSR", 40),
            ("AI Act", "Yüksek risk, şeffaflık", 35),
            ("Sözleşme", "DPA, alt işlemci", 25)),
        Pos("Satınalma", "Stratejik satınalma",
            "AI Core, Object Store, Audit Log sözleşme ve vendor risk.",
            ("Satınalma", "RFQ, müzakere", 40),
            ("Vendor risk", "DPA, SLA, çıkış", 35),
            ("SAP", "Ariba / S/4 alım", 25)),
        Pos("Operasyon", "İş operasyonları",
            "Günlük requisition SLA, kuyruk, hangfire/iş izleme.",
            ("Operasyon", "SLA, vardiya, kalite", 40),
            ("Süreç", "SOP, sapma", 35),
            ("Araç", "Jira, BTP cockpit", 25)),
        Pos("Tedarik Zinciri", "Planlama uzmanı",
            "Çok bölgeli AI Core ve Object Store yerleşimi; gecikme ve maliyet.",
            ("Planlama", "Talep, kapasite", 40),
            ("Lojistik", "Bölge, gecikme", 35),
            ("Veri", "Stok / kuyruk metrikleri", 25)),
        Pos("Üretim", "Dijital üretim mühendisi",
            "S/4 + BTP ile kalite kaydı; HireLens fabrikası için iç İK hattı.",
            ("Üretim", "OEE, hat, vardiya", 40),
            ("S/4HANA", "PP, QM entegrasyon", 35),
            ("Kalite", "Kök neden, 8D", 25)),
        Pos("Kalite", "Kalite güvence uzmanı",
            "Skor-kanıt invariant, mimari test, yük testi. Kırmızı skor yok.",
            ("QA", "Plan, regresyon", 40),
            ("Otomasyon", "xUnit, Playwright", 35),
            ("Uyum", "Kanıt, izlenebilirlik", 25)),
        Pos("BT Destek", "Kurumsal BT uzmanı",
            "IAS, rol koleksiyonu, cihaz ve erişim. Recruiter masaüstü.",
            ("Destek", "L1/L2, SLA", 40),
            ("Kimlik", "IAS, grup, MFA", 35),
            ("Masaüstü", "SSO, tarayıcı", 25)),
        Pos("İş Analizi", "İş analisti",
            "JD kriterleri, ağırlık 100, kanıt cümlesi. İK ve mühendislik arası.",
            ("Analiz", "Gereksinim, kabul", 40),
            ("Modelleme", "Süreç, veri", 35),
            ("Paydaş", "İK, hukuk, ürün", 25)),
        Pos("Yönetim ve Strateji", "İş birimi yöneticisi",
            "Çok kiracılı büyüme, fiyat, risk iştahı. İnsan kararı korunur.",
            ("Strateji", "Pazar, fiyat, ortak", 40),
            ("Yönetişim", "Risk, AI Act, P&L", 35),
            ("Liderlik", "Ekip, öncelik", 25))
    ];

    private static DemoSeedPosition Pos(
        string department,
        string role,
        string jd,
        (string, string, int) a,
        (string, string, int) b,
        (string, string, int) c)
    {
        return new DemoSeedPosition(
            department,
            TitlePrefix + department + " — " + role,
            jd,
            [a, b, c]);
    }

    private static IReadOnlyList<DemoSeedCv> BuildCvs()
    {
        var tracks = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Yazılım Mühendisliği"] = ["API tasarımı", "Entegrasyon", "HANA", "Olay sürümlü", "Kalite"],
            ["Veri ve Analitik"] = ["ETL", "KPI", "Kalite", "Self-serve", "ML ops"],
            ["Bulut ve DevOps"] = ["Platform", "SRE", "Güvenlik", "Maliyet", "Release"],
            ["Siber Güvenlik"] = ["Pentest", "SSDLC", "IAM", "Sırlar", "Olay"],
            ["Ürün Yönetimi"] = ["Discovery", "B2B SaaS", "AI ürün", "Roadmap", "Ölçüm"],
            ["UX ve Tasarım"] = ["Araştırma", "Sistem", "İçerik", "Mobil", "Prototip"],
            ["Satış"] = ["Enterprise", "Kanal", "RFP", "Demo", "Hesap"],
            ["Pazarlama"] = ["PMM", "İçerik", "Etkinlik", "Marka", "ABM"],
            ["Müşteri Başarısı"] = ["Onboarding", "QBR", "Eğitim", "Yenileme", "Escalation"],
            ["İnsan Kaynakları"] = ["TA", "İşveren markası", "Operasyon", "Rapor", "Koçluk"],
            ["Finans"] = ["FP&A", "Maliyet", "Sözleşme", "Audit", "Fiyat"],
            ["Hukuk ve Uyum"] = ["Gizlilik", "AI Act", "Sözleşme", "Denetim", "Politika"],
            ["Satınalma"] = ["RFQ", "Kategori", "Sözleşme", "Risk", "Tasarruf"],
            ["Operasyon"] = ["SLA", "Koordinasyon", "Kalite", "Vardiya", "Rapor"],
            ["Tedarik Zinciri"] = ["S&OP", "Depo", "Taşıma", "Risk", "Sürdürülebilirlik"],
            ["Üretim"] = ["Hat", "MES", "Bakım", "QM", "İyileştirme"],
            ["Kalite"] = ["Test tasarımı", "API", "Performans", "Erişilebilirlik", "Release"],
            ["BT Destek"] = ["Service desk", "IAM", "Uç nokta", "Eğitim", "Olay"],
            ["İş Analizi"] = ["Gereksinim", "Workshop", "Kabul", "Veri", "Değişim"],
            ["Yönetim ve Strateji"] = ["Genel müdür", "Strateji", "Ortaklık", "P&L", "Yönetim kurulu"]
        };

        var list = new List<DemoSeedCv>(ExpectedCount);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var position in Positions)
        {
            var deptTracks = tracks[position.Department];
            for (var track = 0; track < 5; track++)
            {
                for (var level = 0; level < 5; level++)
                {
                    var name = UniqueName(index, usedNames);
                    var city = Cities[index % Cities.Length];
                    var years = level switch
                    {
                        0 => 1,
                        1 => 2,
                        2 => 5,
                        3 => 8,
                        _ => 12
                    };
                    var text = Render(position, deptTracks[track], Levels[level], name, city, years, index);
                    list.Add(new DemoSeedCv(
                        position.Department,
                        position.Title,
                        name,
                        $"{Slug(position.Department)}-{index:D3}.txt",
                        text));
                    index++;
                }
            }
        }

        return list;
    }

    private static string UniqueName(int index, HashSet<string> used)
    {
        for (var offset = 0; offset < 64; offset++)
        {
            var first = FirstNames[(index + offset) % FirstNames.Length];
            var last = LastNames[(index * 3 + offset) % LastNames.Length];
            var name = $"{first} {last}";
            if (used.Add(name))
            {
                return name;
            }
        }

        return $"{FirstNames[index % FirstNames.Length]} {LastNames[index % LastNames.Length]} {index}";
    }

    private static string Slug(string department) =>
        department.ToLowerInvariant()
            .Replace('ı', 'i')
            .Replace('ğ', 'g')
            .Replace('ü', 'u')
            .Replace('ş', 's')
            .Replace('ö', 'o')
            .Replace('ç', 'c')
            .Replace(' ', '-');

    private static string Render(
        DemoSeedPosition position,
        string track,
        string level,
        string name,
        string city,
        int years,
        int index)
    {
        var start = 2026 - years;
        var mail = $"{name.ToLowerInvariant().Replace(' ', '.')}{index}@example.test";
        var skills = string.Join(", ", position.Criteria.Select(c => c.Name));
        return $"""
            ÖZGEÇMİŞ
            Ad: {name}
            Şehir: {city}
            E-posta: {mail}
            Hedef: {position.Department} / {level} — {track}

            ÖZET
            {years} yıl {position.Department.ToLowerInvariant()} deneyimi. 2025-2026 döneminde {track} odağında çalıştı.
            {position.JobDescription}

            DENEYİM
            {start + years - 1}-2026  HireLens / Vesa demo — {level} {track}
            - {skills} kriterlerine uygun teslim.
            - Kanıt cümlesi olmadan sayısal skor üretmedi.
            - KVKK ve AI Act sınırlarını iş tanımında tuttu.

            {start}-{start + Math.Max(years - 2, 1)}  Önceki kurum — {track} uzmanı
            - {Cities[(index + 1) % Cities.Length]} ekibiyle çok lokasyonlu teslim.
            - SAP BTP, S/4HANA Cloud veya eşdeğer kurumsal yığınla çalıştı.

            YETKİNLİKLER
            {skills}; {track}; 2026 araç seti (BTP, HANA Cloud, GitHub Actions, OpenTelemetry).

            EĞİTİM
            {2010 + (index % 10)}  Lisans — ilgili bölüm, Türkiye.

            DİL
            Türkçe (ana dil), İngilizce (iş).
            """;
    }
}
