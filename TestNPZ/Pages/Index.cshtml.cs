using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;
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
        private readonly ApplicationDbContext _context;

        public List<ExchangeBB> ExchangeRates { get; set; }
        public double ResultCalc { get; set; }
        public double StartAmount { get; set; }
        public string StartCurrency { get; set; }
        public string FinishCurrency { get; set; }
        public IndexModel(ILogger<IndexModel> logger, HttpClient httpClient, ApplicationDbContext context)
        {
            _logger = logger;
            _httpClient = httpClient;
            _context = context;
        }
        public void OnPost(string action)
        {
            if (action == "calculate")
            {
                var amount = double.Parse(Request.Form["amount"]);
                var fromCurrency = Request.Form["fromCurrency"];
                var toCurrency = Request.Form["toCurrency"];
                OnPostCalculate(amount, fromCurrency, toCurrency);
            }
            else if (action == "exchange")
            {
                // логика обмена
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
            }
            else
            {
                // Если в сессии нет данных, загрузите их и сохраните в сессию
                ExchangeRates = await GetExchangeAsync("Мозырь");
                HttpContext.Session.SetString("ExchangeRates", JsonConvert.SerializeObject(ExchangeRates));
            }
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
        public async Task<IActionResult> OnPostCalculate(double amount, string fromCurrency, string toCurrency)
        {
            var exch = 1.0;
            StartCurrency = fromCurrency;
            FinishCurrency = toCurrency;
            StartAmount = amount;
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
                        exch = Convert.ToDouble(getRateUSD?.USD_out);
                        break;
                    case "EUR":
                        var getRateEUR = ExchangeRates.FirstOrDefault(x => x.street_type == "ул." && x.street == "Ленинская");
                        exch = Convert.ToDouble(getRateEUR?.EUR_out);
                        break;
                }
                ResultCalc = Math.Round(amount / exch, 2);
            }
            else
            {
                switch (fromCurrency)
                {
                    case "BYN":
                        break;
                    case "USD":
                        var getRateUSD = ExchangeRates.FirstOrDefault(x => x.street_type == "ул." && x.street == "Ленинская");
                        exch = Convert.ToDouble(getRateUSD?.USD_in);
                        break;
                    case "EUR":
                        var getRateEUR = ExchangeRates.FirstOrDefault(x => x.street_type == "ул." && x.street == "Ленинская");
                        exch = Convert.ToDouble(getRateEUR?.EUR_in);
                        break;
                }
                ResultCalc = Math.Round(amount * exch, 2);
            }
            return Page();
        }

        public IActionResult OnPostExchange(decimal amount, string fromCurrency, string toCurrency)
        {
            // Логика для выполнения обмена валюты
            // Это может включать обновление базы данных, учета и т.д.

            // После выполнения обмена, очистите форму
            ModelState.Clear();

            return RedirectToPage();
        }
    }
}
