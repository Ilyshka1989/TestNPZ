using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.CodeAnalysis.Text;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using TestNPZ.Data;
using TestNPZ.Data.Models;
using static System.Net.WebRequestMethods;


namespace TestNPZ.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly HttpClient _httpClient;
        private readonly IServiceProvider _serviceProvider;
        public List<ExchangeBB> ExchangeRates { get; set; }
        public double ResultCalc { get; set; }
        public double StartAmount { get; set; }
        public string StartCurrency { get; set; }
        public string FinishCurrency { get; set; }

        public List<Logging> LoggingsA { get; set; }
        
        public IndexModel(ILogger<IndexModel> logger, HttpClient httpClient, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _httpClient = httpClient;
            _serviceProvider = serviceProvider;
        }
        public void OnPost(string action)
        {
            if (action == "calculate")
            {
                var amount = decimal.Parse(Request.Form["amount"]);
                var fromCurrency = Request.Form["fromCurrency"];
                var toCurrency = Request.Form["toCurrency"];
                OnPostCalculate(amount, fromCurrency, toCurrency);
            }
            else if (action == "exchange")
            {
                var amount = decimal.Parse(Request.Form["amount"]);
                var fromCurrency = Request.Form["fromCurrency"];
                var toCurrency = Request.Form["toCurrency"];
                OnPostCalculate(amount, fromCurrency, toCurrency);
                OnPostExchange();
            }
        }
        public async Task OnGet()
        {
            // Попробуйте извлечь курсы валют из сессии
            var exchangeRatesData = HttpContext.Session.GetString("ExchangeRates");
            if (!string.IsNullOrEmpty(exchangeRatesData))
            {
                // Десериализация данных из сессии
                ExchangeRates = JsonConvert.DeserializeObject<List<ExchangeBB>>(exchangeRatesData);
                LoggingsA = await GetAsyncLog();
            }
            else
            {
                // Если в сессии нет данных, загрузите их и сохраните в сессию
                LoggingsA = await GetAsyncLog();
                ExchangeRates = await GetExchangeAsync("Мозырь");
                
                HttpContext.Session.SetString("ExchangeRates", JsonConvert.SerializeObject(ExchangeRates));
            }
        }
        public bool CheckDate(DateTime d1, DateTime d2)
        {
            return new DateTime(d1.Year, d1.Month, d1.Day) == new DateTime(d2.Year, d2.Month, d2.Day);
        }
        public async Task<List<Logging>> GetAsyncLog() 
        {
            var result = new List<Logging>();
            using (var scope = _serviceProvider.CreateScope())
            {
                var contextOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
                using (var context = new ApplicationDbContext(contextOptions))
                {
                    result = await context.Loggings.Where(x => x.Id > 0).ToListAsync();
                }
                
            }
            return result;
        }
        public async Task<List<ExchangeBB>> GetExchangeAsync(string city = "Минск")
        {
            var result = new List<ExchangeBB>();
            string uri = $"https://belarusbank.by/api/kursExchange?city={city}";

            try
            {
                    // Здесь используется асинхронный вызов для получения ответа
                    HttpResponseMessage response = await _httpClient.GetAsync(uri);
                    if (response.IsSuccessStatusCode)
                    {
                        string documentContents = await response.Content.ReadAsStringAsync();
                        result = JsonConvert.DeserializeObject<List<ExchangeBB>>(documentContents);
                    }
                    else
                    {
                        _logger.LogError($"Ошибка при запросе: {response.StatusCode}");
                    }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении курса валют");
            }

            return result;
        }
        public async void OnPostCalculate(decimal amount, string fromCurrency, string toCurrency)
        {
            var exch = 1.0;
            StartCurrency = fromCurrency;
            FinishCurrency = toCurrency;
            StartAmount = Convert.ToDouble(amount); 
            var exchangeRatesData = HttpContext.Session.GetString("ExchangeRates");
            if (!string.IsNullOrEmpty(exchangeRatesData))
            {
                ExchangeRates = JsonConvert.DeserializeObject<List<ExchangeBB>>(exchangeRatesData);
            }

            if (fromCurrency == "BYN")
            {
                switch (toCurrency)
                {
                    case "BYN":
                        break;
                    case "USD":
                        var getRateUSD = ExchangeRates.FirstOrDefault(x => x.street_type == "ул." && x.street == "Ленинская");
                        exch = Convert.ToDouble(getRateUSD?.USD_out.Replace('.', ',')); break;
                    case "EUR":
                        var getRateEUR = ExchangeRates.FirstOrDefault(x => x.street_type == "ул." && x.street == "Ленинская");
                        exch = Convert.ToDouble(getRateEUR?.EUR_out.Replace('.', ','));
                        break;
                }
                ResultCalc = Math.Round(Convert.ToDouble(amount) / exch, 2);
            }
            else
            {
                switch (fromCurrency)
                {
                    case "BYN":
                        break;
                    case "USD":
                        var getRateUSD = ExchangeRates.FirstOrDefault(x => x.street_type == "ул." && x.street == "Ленинская");
                        exch = Convert.ToDouble(getRateUSD?.USD_in.Replace('.', ',')); break;
                    case "EUR":
                        var getRateEUR = ExchangeRates.FirstOrDefault(x => x.street_type == "ул." && x.street == "Ленинская");
                        exch = Convert.ToDouble(getRateEUR?.EUR_in.Replace('.', ','));

                        break;
                }
                ResultCalc = Math.Round(Convert.ToDouble(amount) * exch, 2);
            }
        }
        public async void OnPostExchange()
        {
            var userName = User.Identity.Name == null ? "" : User.Identity.Name;
            var log = new Logging();
            log.CreatedDate = DateTime.Now;
            log.UserName = userName;
            log.RowMessage = " Оператор " + userName + " произвел обмен " + StartAmount + " в " + FinishCurrency + " на сумму " +  ResultCalc;
            using (var scope = _serviceProvider.CreateScope())
            {
                var contextOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
                using (var context = new ApplicationDbContext(contextOptions))
                {
                    context.Loggings.Add(log);
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}
