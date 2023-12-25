using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using TestNPZ.Data.Models;
using static System.Net.WebRequestMethods;

namespace TestNPZ.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly HttpClient _httpClient;
        public List<ExchangeBB> ExchangeRates { get; set; }
        public IndexModel(ILogger<IndexModel> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task OnGet()
        {
            ExchangeRates = await GetExchangeAsync("Мозырь");
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
    }
}
