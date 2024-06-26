# Телеметрия в .NET: как затащить быстро и почти бесплатно

Телеметрия — это инструменты для наблюдения за приложением. В этом проекте даны примеры использования трёх инструментов телеметрии в распределённой системе на .NET: логирование, метрики и трассировка. Телеметрия добавляется в тестовый проект интернет-магазина (только backend). Для телеметрии используются только бесплатные инструменты с открытым исходным кодом. Тестовые микросервисы и все инструменты телеметрии поднимаются с помощью [docker-compose](https://docs.docker.com/compose/).

## Тестовая система

Тестовая система состоит из трёх микросервисов на ASP.NET Web API:

* [Payments](Payments/Program.cs) — пример сервиса для проведения оплаты. На каждый запрос генерирует случайные данные о пользователе и его балансе. Затем проверяет, достаточно ли у пользователя средств для совершения покупки. Если денег хватает, возвращает успешный ответ. Если нет, возвращает ошибку 400.
* [Stock](Stock/Program.cs) — пример сервиса для работы со складом. Может возвращать список продуктов, доступных на складе, и позволяет бронировать определённый продукт. При бронировании продукта случайным образом определяет, доступен ли он на складе. Если доступен, то возвращает успешный ответ. Если нет, возвращает ошибку 400.
* [Shop](Shop/Program.cs) — пример сервиса витрины интернет-магазина. Позволяет получить список товаров со склада и принимает запросы на покупку продукта. Перед покупкой проверяет, доступен ли продукт на складе. Если доступен, то проводит оплату. Если оплата прошла успешно, то возвращает успешный ответ. Если продукт недоступен или оплата не прошла, возвращает ошибку 400.

Кроме этого, есть тестовый клиент [LoadTest](LoadTest/Program.cs), который бесконечно отправляет запросы на покупку продукта в магазин.

Запустить тестовую систему можно с помощью docker-compose:

```bash
docker-compose up
```

Системы начнут обмениваться запросами (честное слово!), но мы об этом не узнаем, т. к. у нас нет никакой телеметрии. Остановить систему можно с помощью `Ctrl+C`.

## Логирование

Логи — это краткие текстовые записи, описывающие происходящее в системе. Помимо текста логи содержат следующую информацию:

* Уровень логирования, который показывает важность сообщения. Например, уровень `Error` — это сообщение об ошибке, которое нужно обязательно проверить. Уровень `Information` — это обычное сообщение, которое нужно проверить, если что-то не работает.
* Дата и время сообщения, которые позволяют сопоставить события по времени их происхождения.
* Дополнительная структурированная информация, которая может быть полезна при поиске ошибок. Например, в логах могут быть записаны имя пользователя, который совершил действие, или идентификатор запроса. Обычно эта информация записывается в формате JSON и обрабатывается системой хранения логов.

### Добавление логов

Добавим логирование в нашу систему. Для этого в каждый из проектов потребуется добавить следующие NuGet-пакеты (сейчас можно не выполнять — они уже добавлены в проекты):

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Extensions.Logging
dotnet add package Serilog.Sinks.Seq
```

Теперь сконфигурируем логирование в каждом из проектов. Для этого в каждом проекте в `Program.cs` добавим следующий код:

```csharp
builder.Host.UseSerilog((_, loggerConfiguration) => loggerConfiguration
    .MinimumLevel.Debug()         // Минимальный уровень логирования. Логи с меньшим уровнем не будут записаны
	.WriteTo.Console()            // Пишем логи в консоль
	.WriteTo.Seq("http://seq"));  // Отправляем логи в систему хранения логов Seq
```

После этого в обработчиках запросов мы можем писать логи. Для этого нужно добавить в каждый обработчик параметр `ILogger` с атрибутом `FromServices`. Далее, внутри обработчика можно вызывать методы этого объекта, соответствующие уровню логирования:

```csharp
app.MapGet("/products", ([FromServices] ILogger logger) =>
{
	logger.Debug("Getting products"); // Логируем сообщение с уровнем Debug
	return products;
});
```

Для записи структурной информации используются фигурные скобки:

```csharp
logger.Warning("Product {Product} not found", product); // Добавляем логу свойство Product
```

Чтобы записать сложный объект, в фигурные скобки добавляется @ (без этого объект будет приводиться к строке методом `ToString()`):

```csharp
record ClientAccount(
	string ClientName,
	decimal Balance);

var clientAccount = GetClientAccount(sum);
logger.Debug("Paying {Sum} roubles from the account {@ClientAccount}", sum, clientAccount);
```

### Просмотр логов

Запустим систему:

```bash
docker-compose up
```

Теперь мы увидим в консоли информацию об обработке запросов:

```log
[04:24:41 DBG] Paying 2496 roubles from the account {"ClientName": "Yoko", "Balance": 23419, "$type": "ClientAccount"}
```

Система для хранения логов Seq тоже поднята в docker-compose. Она доступна по адресу [http://localhost:5341](http://localhost:5341). Напишем в строке поиска запрос на поиск платежей по имени клиента:

```
ClientAccount.ClientName = 'Yoko'
```

Seq выдаст нам все логи о платежах этого клиента. Также поддерживаются более сложные запросы:

```
ClientAccount.ClientName = 'Yoko' and ClientAccount.Balance < 10000
```

Подробнее о языке запросов можно прочитать в [документации](https://docs.datalust.co/docs/the-seq-query-language).

### Маскирование логов

Имя пользователя и его баланс — это чувствительная информация, и её нельзя отображать в логах. Для этого применяется маскирование. Для маскирования будем использовать библиотеку Destructarama. Добавим её в проекты:

```bash
dotnet add package Destructurama.Attributed
```

Библиотека использует атрибуты, которыми можно помечать маскируемые свойства:

```csharp
record ClientAccount
(
    // Маскируем текст строкой вида «N**e»
	[property: LogMasked(PreserveLength = true, ShowFirst = 1, ShowLast = 1)]
	string ClientName,

    // Исключаем число из логов
	[property: NotLogged]
	decimal Balance
);
```

Для включения маскирования нужно добавить в конфигурацию логирования последнюю строку:

```csharp
builder.Host.UseSerilog((_, loggerConfiguration) => loggerConfiguration
	.MinimumLevel.Debug()
	.WriteTo.Console()
	.WriteTo.Seq("http://seq")
	.Destructure.UsingAttributes()); // Включаем маскирование
```

Снова запустим систему:

```bash
docker-compose up
```

Мы увидим, что имя клиента теперь замаскировано, а сумма денег на его балансе вообще не отображается:

```log
[04:41:46 DBG] Paying 3130 roubles from the account {"ClientName": "K***h", "$type": "ClientAccount"}
```

Аналогичная картина будет [и в Seq](http://localhost:5341).

### Самодельная выгрузка логов

Иногда возникает необходимость настроить выгрузку логов в систему, для которой ещё нет поддержки в Serilog. Для выгрузки можно реализовать интерфейс `ILogEventSink` с необходимой логикой. В примере ниже мы считаем статистику по логам:

```csharp
class StatisticsSink : ILogEventSink
{
	static readonly ConcurrentDictionary<LogEventLevel, int> _statistics = new();
	
	public void Emit(LogEvent logEvent)
	{
		_statistics.AddOrUpdate(logEvent.Level, 1, (_, count) => count + 1);
	}
	
	public static IReadOnlyDictionary<LogEventLevel, int> Statistics => _statistics;
}
```

Зарегистрируем нашу реализацию в конфигурации логирования:

```csharp
builder.Host.UseSerilog((_, loggerConfiguration) => loggerConfiguration
	.MinimumLevel.Debug()
	.WriteTo.Console()
	.WriteTo.Seq("http://seq")
	.WriteTo.Sink<StatisticsSink>()); // Добавляем эту строку, чтобы собирать статистику логов
```

Добавим в наш API метод, возвращающий собранную статистику:

```csharp
app.MapGet("/log/statistics", () => StatisticsSink.Statistics);
```

Снова запустим систему:

```bash
docker-compose up
```

Перейдя к реализованному методу в сервисе склада, увидим собранную статистику: [http://localhost:8280/log/statistics](http://localhost:8280/log/statistics)

## Метрики

Метрики — это статистическая информация об использовании системы. Метрики отдаются в агрегированном текстовом виде и бывают разных типов:

* Counter — счетчик, постоянно увеличивающийся в течение времени. Пример: количество поступивших запросов.
* Gauge — показатель, который может увеличиваться и уменьшаться. Пример: количество активных пользователей за день.
* Histogram — гистограмма, показывающая распределение значений. Пример: время обработки запроса.

Могут быть и другие типы метрик, но эти три наиболее часто встречающиеся.

Кроме значения каждая метрика может содержать произвольный набор тегов, которые позволяют группировать метрики. Например, можно собирать метрики по каждому клиенту, чтобы понимать, какие клиенты чаще всего обращаются к системе.

### Добавление метрик

Добавим метрики в нашу систему. Для этого в каждый из проектов потребуется добавить следующий NuGet-пакет:

```bash
dotnet add package prometheus-net.AspNetCore
```

Теперь сконфигурируем метрики в каждом из проектов. Для этого в каждом проекте в `Program.cs` добавим следующий код:

```csharp
app.UseMetricServer(url: "/metrics");  // Метрики будут публиковаться сервисом на этом URL
app.UseHttpMetrics();                  // Включаем метрики HTTP, предоставляемые библиотекой
```

Сами обработчики запросов модифицировать не нужно — библиотека сама соберёт метрики. Запустим систему:

```bash
docker-compose up
```

Метрики можно увидеть по путям:

* Shop — [http://localhost:8180/metrics](http://localhost:8180/metrics)
* Stock — [http://localhost:8280/metrics](http://localhost:8280/metrics)
* Payments — [http://localhost:8380/metrics](http://localhost:8380/metrics)

Пример метрики:

```
http_request_duration_seconds_sum{code="200",method="GET",controller="",action="",endpoint="/products"} 0.20988220000000002
```

Здесь:

* `http_request_duration_seconds_sum` — название метрики. Эта метрика — суммарное время обработки запросов, описанных в тегах.
* В фигурных скобках перечислены теги, описывающие конкретный запрос:
  * `code` — код ответа (в примере — 200).
  * `method` — метод запроса (в примере — `GET`).
  * `endpoint` — путь запроса (в примере — `/products`).
* После тегов идёт значение метрики — суммарное время обработки запросов в секундах.

Конечно, изучать метрики глазами не очень удобно — дальше посмотрим, как их собирать и визуализировать.

### Сбор метрик

Для сбора и хранения метрик будем использовать Prometheus. Он добавлен в docker-compose. Кроме этого, для работы сбора метрик требуется его настроить в файле `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'shop'
    scrape_interval: 5s # метрики собираются каждые 5 секунд
    static_configs:
      - targets: ['shop:8080'] # адрес сервиса внутри Docker
```

Запустим систему:

```bash
docker-compose up
```

Собранные метрики можно посмотреть в интерфейсе Prometheus по адресу [http://localhost:9090/graph?g0.expr=http_request_duration_seconds_sum](http://localhost:9090/graph?g0.expr=http_request_duration_seconds_sum) (дан пример запроса метрики, рассмотренной выше).

### Визуализация метрик

Для визуализации метрик будем использовать Grafana. Он добавлен в docker-compose. Все настройки сделаны в графическом интерфейсе и сохранены в папке `grafana-data`. Чтобы посмотреть подготовленную визуализацию, запустим систему:

```bash
docker-compose up
```

Перейдём в интерфейс Grafana по адресу [http://localhost:3000](http://localhost:3000) (логин — `admin`, пароль — `admin`). На доске представлены два графика: 95-й процентиль времени обработки запросов и количество запросов с ошибками в секунду.

### Самодельные метрики

В качестве примера реализуем бизнес-метрику, которая подсчитывает, сколько денег потратили пользователи в нашем магазине. Для этого в проекте `Shop` добавим следующий код:

```csharp
var moneyCounter = Metrics.CreateCounter("money_spent", "Money spent on products", "currency");

app.MapPost("/products/buy", async (string product, HttpClient httpClient, [FromServices] ILogger logger) =>
{
	var sum = Random.Shared.Next(100, 10000);
	
	// Осуществление покупки

	moneyCounter.WithLabels("roubles").Inc(sum); // Добавляем сумму к счетчику
	
    // Логирование и возврат результата
});
```

Запустим систему:

```bash
docker-compose up
```

Перейдём в интерфейс Grafana по адресу [http://localhost:3000](http://localhost:3000) (логин — `admin`, пароль — `admin`). Там уже добавлена визуализация нашей метрики.

## Трассировка

Трасса (trace) — это визуализация прохождения запроса по системе. Трассировка позволяет понять, какие запросы были отправлены, какие ответы были получены, какие сервисы были вызваны, какие ошибки возникли. Трасса состоит из множества отрезков (spans). Каждый отрезок — это часть запроса, которая была обработана в системе. Например, это может быть запрос к базе данных, вызов метода в другом сервисе, обработка запроса в сервисе. Отрезок имеет время начала и конца, а также может содержать дополнительную информацию, например, имя сервиса, который обрабатывал запрос.

### Добавление трасс

Добавим трассы в нашу систему. Для этого в каждый из проектов потребуется добавить следующие NuGet-пакеты:

```bash
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
```

Теперь сконфигурируем трассы в каждом из проектов. Для этого в каждом проекте в `Program.cs` добавим следующий код:

```csharp
builder.Services
	.AddOpenTelemetry()
	.ConfigureResource(resource => resource.AddService("payments"))
	.WithTracing(tracing => tracing
		.AddAspNetCoreInstrumentation()  // Включаем трассировку HTTP, предоставляемую библиотекой
		.AddOtlpExporter(options => options.Endpoint = new Uri("http://jaeger:4317"))); // Отправляем трассы в сервис Jaeger
```

Мы будем собирать трассы в Jaeger. Он уже добавлен в docker-compose. Запустим систему:

```bash
docker-compose up
```

Перейдём в Jaager по адресу [http://localhost:16686](http://localhost:16686). В поле `Service` выберем `shop`, а в поле `Operation` — `POST /products/buy`. Нажмём кнопку `Find Traces`. В результате мы увидим список трасс, которые были собраны.

### Добавление своих отрезков

Чтобы добавлять свои отрезки (например, для большей детализации трасс), в проекте `Shop` добавим следующий код:

```csharp
app.MapPost("/products/buy", async (string product, HttpClient httpClient, [FromServices] ILogger logger) =>
{
	// Подготовка к обработке запроса

	ActivitySource mySource = new("my-source"); // Создаём источник отрезков

	using (mySource.StartActivity("reservation", ActivityKind.Client))
	{
		// Выполнение запроса к сервису Stock для резервирования товара
	}
	
	using (mySource.StartActivity("paying", ActivityKind.Client))
	{
		// Выполнение запроса к сервису Payments для оплаты товара
	}

	// Логирование и возврат результата
});
```

Кроме этого, в конфигурации необходимо зарегистрировать новый источник отрезков:

```csharp
builder.Services
	.AddOpenTelemetry()
	.ConfigureResource(resource => resource.AddService("shop"))
	.WithTracing(tracing => tracing
		.AddAspNetCoreInstrumentation()
		.AddSource("my-source") // Регистрируем источник отрезков
		.AddOtlpExporter(options => options.Endpoint = new Uri("http://jaeger:4317")));
```

Снова запустим систему:

```bash
docker-compose up
```

Перейдём в Jaager по адресу [http://localhost:16686](http://localhost:16686). В поле `Service` выберем `shop`, а в поле `Operation` — `POST /products/buy`. Нажмём кнопку `Find Traces`. Выбрав одну из показанных трасс, мы увидим, что 2 отрезка, которые мы добавили, теперь отображаются в трассе.

### Связывание трасс с логами

Очень удобно добавить возможность перехода от логов к трассам. Для этого реализуем интерфейс `ILogEventEnricher` в проекте `Shop`:

```csharp
class OpenTelemetryEnricher : ILogEventEnricher
{
	public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
	{
		var currentActivity = Activity.Current;
		if (currentActivity == null)
			return;

        // Добавим в лог свойства, содержащие идентификаторы трассы и отрезка:
		logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", currentActivity.TraceId));
		logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", currentActivity.SpanId));
		
        // Добавим в лог ссылку на трассу в Jaeger:
		logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceLink", $"http://localhost:16686/trace/{currentActivity.TraceId}"));
	}
}
```

Зарегистрируем наш enricher в конфигурации логирования:

```csharp
builder.Host.UseSerilog((_, loggerConfiguration) => loggerConfiguration
	.MinimumLevel.Debug()
	.Enrich.With<OpenTelemetryEnricher>() // Регистрируем наш enricher
	.WriteTo.Console()
	.WriteTo.Seq("http://seq"));
```

Снова запустим систему:

```bash
docker-compose up
```

Перейдём в Seq по адресу [http://localhost:5341](http://localhost:5341). В поле `Search` введём `Has(TraceLink)`. В результате мы увидим список логов, которые содержат ссылку на трассу. Перейдя по ссылке, мы попадём в Jaeger и увидим трассу, которая была собрана для этого запроса.
