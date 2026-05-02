using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using InstaCurator.Models;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Telegram.Bot;
using Telegram.Bot.Types;

// ALIAS IMAGESHARP
using SLImage = SixLabors.ImageSharp.Image;
using SLColor = SixLabors.ImageSharp.Color;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;

namespace InstaCurator
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<PostInstagram> MeusPosts { get; set; }
        public ObservableCollection<string> RivaisSalvos { get; set; }
        public ObservableCollection<PostInstagram> PinsEncontrados { get; set; }

        public ObservableCollection<SlideCuriosidade> SlidesCarrossel { get; set; }

        

        private ITelegramBotClient botClient;
        private static readonly HttpClient httpClient = new HttpClient();
        private string caminhoRivais = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rivais.txt");

        private bool _isDragging = false;
        private Point _clickPosition;
        private double _limiteMinX = 0;
        private double _limiteMinY = 0;


        private Dictionary<string, List<string>> _cacheBuscaBing = new Dictionary<string, List<string>>();
        private Dictionary<string, int> _indiceBuscaBing = new Dictionary<string, int>();

        private string _botToken = "";
        private long _chatId = 0;
        private string _groqKey = "";
        private string _unsplashApiKey = "";
        private string _pexelsApiKey = "";
        private string _rapidApiKey = "";
        private const string ApiHost = "instagram-downloader-scraper-reels-igtv-posts-stories.p.rapidapi.com";



        private string caminhoConfig = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config_equipe.json");

        public MainWindow()
        {
            InitializeComponent();
            CarregarConfiguracoesLocais();
            MeusPosts = new ObservableCollection<PostInstagram>();
            RivaisSalvos = new ObservableCollection<string>();
            PinsEncontrados = new ObservableCollection<PostInstagram>();
            SlidesCarrossel = new ObservableCollection<SlideCuriosidade>();

            this.DataContext = this;
            ListaPosts.ItemsSource = MeusPosts;
            ListaRivais.ItemsSource = RivaisSalvos;
            ListaPins.ItemsSource = PinsEncontrados;
            ListaSlidesEditor.ItemsSource = SlidesCarrossel;

            CarregarRivais();
            httpClient.Timeout = TimeSpan.FromSeconds(120);
            
            httpClient.DefaultRequestHeaders.Add("User-Agent", "InstaCurator-Agencia/1.0");
        }

        private void CarregarConfiguracoesLocais()
        {
            if (System.IO.File.Exists(caminhoConfig))
            {
                try
                {
                    var json = System.IO.File.ReadAllText(caminhoConfig);
                    dynamic config = Newtonsoft.Json.JsonConvert.DeserializeObject(json);


                    _groqKey = config.GroqKey;
                    _botToken = config.BotToken;
                    _chatId = config.ChatId;


                    _unsplashApiKey = config.UnsplashKey;
                    _pexelsApiKey = config.PexelsKey;
                    _rapidApiKey = config.RapidApiKey;

                    if (!string.IsNullOrEmpty(_botToken))
                    {
                        botClient = new TelegramBotClient(_botToken);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao ler o arquivo de configuração: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Arquivo config_equipe.json não encontrado! Crie o arquivo na pasta do aplicativo.", "Aviso Sênior");
            }
        }




        // ==========================================
        // UTILITÁRIOS GERAIS
        // ==========================================
        private void CarregarRivais() { if (System.IO.File.Exists(caminhoRivais)) { var linhas = System.IO.File.ReadAllLines(caminhoRivais); foreach (var linha in linhas) RivaisSalvos.Add(linha); } }
        private void BtnLimparFeed_Click(object sender, RoutedEventArgs e) { if (MessageBox.Show("Limpar tudo?", "Confirmação", MessageBoxButton.YesNo) == MessageBoxResult.Yes) MeusPosts.Clear(); }
        private void BtnSalvarRival_Click(object sender, RoutedEventArgs e) { string novo = TxtRival.Text.Replace("@", "").Trim(); if (!string.IsNullOrEmpty(novo) && !RivaisSalvos.Contains(novo)) { RivaisSalvos.Add(novo); System.IO.File.AppendAllLines(caminhoRivais, new[] { novo }); } }
        private void ListaRivais_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (ListaRivais.SelectedItem != null) TxtRival.Text = ListaRivais.SelectedItem.ToString(); }
        private void MatarChromesZumbis() { try { foreach (var proc in System.Diagnostics.Process.GetProcessesByName("chromedriver")) proc.Kill(); } catch { } }

        // ==========================================
        // MOTOR: INSTAGRAM
        // ==========================================
        private async void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            string rival = TxtRival.Text.Replace("@", "").Trim(); int.TryParse(TxtMinLikes.Text, out int minLikes); if (string.IsNullOrEmpty(rival)) return;
            MatarChromesZumbis(); BtnBuscar.Content = "Buscando... 🤖"; BtnBuscar.IsEnabled = false;
            await Task.Run(() => ExecutarScraper(rival, minLikes));
            BtnBuscar.Content = "Buscar Instagram 🔍"; BtnBuscar.IsEnabled = true;
        }

        private void ExecutarScraper(string usuario, int likesAlvo)
        {
            var options = new ChromeOptions();
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            options.AddArgument("--window-size=1280,800"); options.AddArgument("--window-position=-32000,-32000");
            string pastaPerfil = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InstaCurator", "ChromeProfile");
            options.AddArgument($"user-data-dir={pastaPerfil}");

            IWebDriver driver = null;
            try
            {
                driver = new ChromeDriver(options);
                driver.Navigate().GoToUrl($"https://www.instagram.com/{usuario}/"); System.Threading.Thread.Sleep(4000);
                if (driver.Url.Contains("login")) return;

                var posts = driver.FindElements(By.XPath("//a[contains(@href, '/p/') or contains(@href, '/reel/')]"));
                foreach (var post in posts.Take(12))
                {
                    try
                    {
                        var img = post.FindElement(By.TagName("img")); string alt = img.GetAttribute("alt") ?? ""; string src = img.GetAttribute("src"); string href = post.GetAttribute("href");
                        if (!href.StartsWith("http")) href = "https://www.instagram.com" + href;
                        int likes = ExtrairLikes(alt);
                        if (likes >= likesAlvo) { Application.Current.Dispatcher.Invoke(() => { if (!MeusPosts.Any(p => p.LinkPost == href)) MeusPosts.Insert(0, new PostInstagram { PaginaRival = "@" + usuario, Legenda = alt, QuantidadeLikes = likes, CaminhosDasImagens = new List<string> { src }, LinkPost = href }); }); }
                    }
                    catch { }
                }
            }
            finally { if (driver != null) { driver.Quit(); driver.Dispose(); } }
        }

        private int ExtrairLikes(string texto) { var match = Regex.Match(texto ?? "", @"([\d\.,]+)\s+(curtidas|likes)"); if (match.Success) return int.Parse(match.Groups[1].Value.Replace(".", "").Replace(",", "")); return 0; }
        private void BtnDescartar_Click(object sender, RoutedEventArgs e) { if (((FrameworkElement)sender).DataContext is PostInstagram p) MeusPosts.Remove(p); }
        private void BtnAbrirLink_Click(object sender, RoutedEventArgs e) { if (((FrameworkElement)sender).DataContext is PostInstagram p) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = p.LinkPost, UseShellExecute = true }); }

        private async void BtnAprovar_Click(object sender, RoutedEventArgs e)
        {
            var botao = sender as Button; var post = botao?.DataContext as PostInstagram; if (post == null || !botao.IsEnabled) return;
            botao.IsEnabled = false; botao.Content = "Enviando... 🚀";
            try
            {
                List<string> linksUnicos = await ObterLinksPelaAPI(post.LinkPost);
                if (linksUnicos.Count > 0)
                {
                    var album = new List<IAlbumInputMedia>();
                    foreach (var url in linksUnicos.Take(10)) { if (url.Contains(".mp4")) album.Add(new InputMediaVideo(InputFile.FromUri(url))); else album.Add(new InputMediaPhoto(InputFile.FromUri(url))); }
                    await botClient.SendMediaGroupAsync(_chatId, album); MeusPosts.Remove(post);
                }
            }
            catch (Exception ex) { MessageBox.Show("Erro: " + ex.Message); }
            finally { if (botao != null) { botao.Content = "Aprovar 🚀"; botao.IsEnabled = true; } }
        }

        private async Task<List<string>> ObterLinksPelaAPI(string urlDoPost)
        {
            var finalLinks = new List<string>(); var fingerprints = new HashSet<string>();
            var request = new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = new Uri($"https://{ApiHost}/scraper?url={Uri.EscapeDataString(urlDoPost)}"), Headers = { { "x-rapidapi-key", _rapidApiKey }, { "x-rapidapi-host", ApiHost } }, };
            using (var response = await httpClient.SendAsync(request))
            {
                if (!response.IsSuccessStatusCode) return finalLinks;
                string json = (await response.Content.ReadAsStringAsync()).Replace("\\/", "/"); var matches = Regex.Matches(json, @"https?://[^""\s>]+");
                foreach (Match match in matches) { string url = match.Value; if (url.Contains(".jpg") || url.Contains(".jpeg") || url.Contains(".webp") || url.Contains(".mp4")) { if (!url.Contains("150x150") && !url.Contains("profile_pic")) { string filename = url.Split('?')[0].Split('/').Last(); if (!fingerprints.Contains(filename)) { fingerprints.Add(filename); finalLinks.Add(url); } } } }
            }
            return finalLinks;
        }

        // ==========================================
        // MOTOR: PINTEREST
        // ==========================================
        private async void BtnBuscarPinterest_Click(object sender, RoutedEventArgs e)
        {
            string busca = TxtBuscaPinterest.Text.Trim(); if (string.IsNullOrEmpty(busca)) return;
            MatarChromesZumbis(); BtnBuscarPinterest.Content = "Buscando... 📌"; BtnBuscarPinterest.IsEnabled = false; PinsEncontrados.Clear();
            await Task.Run(() => {
                var options = new ChromeOptions(); options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"); options.AddArgument("--window-position=-32000,-32000");
                string pastaPerfil = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InstaCurator", "ChromeProfile"); options.AddArgument($"user-data-dir={pastaPerfil}");
                IWebDriver driver = null;
                try
                {
                    driver = new ChromeDriver(options); driver.Navigate().GoToUrl($"https://br.pinterest.com/search/pins/?q={Uri.EscapeDataString(busca)}&rs=typed"); System.Threading.Thread.Sleep(8000);
                    var imgs = driver.FindElements(By.TagName("img")); var linksUnicos = new HashSet<string>();
                    foreach (var img in imgs) { string src = img.GetAttribute("src"); if (!string.IsNullOrEmpty(src) && src.Contains("i.pinimg.com") && !src.Contains("avatars")) { string original = Regex.Replace(src, @"/(?:236x|474x|736x)/", "/originals/"); if (linksUnicos.Add(original)) { Application.Current.Dispatcher.Invoke(() => PinsEncontrados.Add(new PostInstagram { CaminhosDasImagens = new List<string> { original }, LinkPost = original, Legenda = busca })); } } if (linksUnicos.Count >= 24) break; }
                }
                finally { if (driver != null) { driver.Quit(); driver.Dispose(); } }
            });
            BtnBuscarPinterest.Content = "Buscar Pins 📌"; BtnBuscarPinterest.IsEnabled = true;
        }

        private async void BtnEnviarPin_Click(object sender, RoutedEventArgs e)
        {
            var botao = sender as Button; var pin = botao?.DataContext as PostInstagram; if (pin == null || !botao.IsEnabled) return;
            botao.IsEnabled = false; botao.Content = "Enviando... ⏳";
            try { await botClient.SendPhotoAsync(_chatId, InputFile.FromUri(pin.CaminhosDasImagens[0])); PinsEncontrados.Remove(pin); } catch { botao.IsEnabled = true; botao.Content = "Telegram 🚀"; }
        }

        // ==========================================
        // MOTOR: EDITOR MINI-CANVA COM FEW-SHOT AI
        // ==========================================

        private async void BtnGerarRoteiroBase_Click(object sender, RoutedEventArgs e)
        {
            string tema = TxtTemaCuriosidade.Text.Trim();
            if (string.IsNullOrEmpty(tema)) return;

            OverlayProgresso.Visibility = Visibility.Visible;
            TxtProgressoStatus.Text = "Conectando com a IA... 🧠";
            BarraProgresso.Value = 10;
            BtnGerarRoteiroBase.IsEnabled = false;

            try
            {
                string tomSelecionado = ((ComboBoxItem)CmbTom.SelectedItem).Content.ToString();
                string fonteSelecionada = ((ComboBoxItem)CmbFonteImagem.SelectedItem).Content.ToString();


                string diretrizCopy = tomSelecionado.Contains("Extremo") ? "Tom APELATIVO e EXTREMO. Use gatilhos de choque." :
                                      tomSelecionado.Contains("Sombrio") ? "Tom SOMBRIO e de SUSPENSE." :
                                      tomSelecionado.Contains("Emocional") ? "Tom EMOCIONAL e INSPIRADOR." : "Tom INFORMATIVO e DINÂMICO.";

                string regraImagens = fonteSelecionada.Contains("IA")
                    ? "Escreva 'Prompts de IA' curtos e hiper-focados em INGLÊS. Foco total em cinematografia, dark fantasy, sem texto na imagem."
                    : "Gere palavras-chave literais em INGLÊS focadas no cenário físico ou objeto.";

                
                string schemaEsperado = @"
{
  ""titulo_capa"": ""TÍTULO FORTE AQUI"",
  ""subtitulo_capa"": ""SUBTÍTULO AQUI"",
  ""keyword_capa"": ""Cenario 1 em ingles (ex: fachada de um predio dark)"",
  ""slides"": [
    { ""fato"": ""Texto da curiosidade com 30 a 45 palavras."", ""keyword"": ""Cenario 2 em ingles (MUDE O AMBIENTE. ex: sala escura)"" },
    { ""fato"": ""Outro texto da curiosidade..."", ""keyword"": ""Cenario 3 em ingles (MUDE O AMBIENTE. ex: close up de um objeto)"" }
  ],
  ""legenda"": ""Sua legenda magnética aqui""
}";

                var messages = new[] {
            new { role = "system", content = $@"Você é um Roteirista Especialista em 'Storytelling Viral' para Instagram. 
Sua missão é criar carrosséis de 5 a 6 slides.
{diretrizCopy}
{regraImagens}
REGRAS: 30 a 45 palavras por slide. Sem emojis. 
OBRIGATÓRIO: Retorne a sua resposta EXATAMENTE com a seguinte estrutura JSON. Não altere os nomes das chaves:
{schemaEsperado}" },
            new { role = "user", content = $"Tema: {tema}" }
        };

                var requestBody = new { model = "llama-3.3-70b-versatile", messages = messages, response_format = new { type = "json_object" }, temperature = 0.4 };
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions")
                {
                    Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(requestBody), System.Text.Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Authorization", $"Bearer {_groqKey}");

                var response = await httpClient.SendAsync(request);
                string jsonRes = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"A API da IA recusou o pedido (Erro {response.StatusCode}).\nDetalhes:\n{jsonRes}", "Erro na IA");
                    return;
                }

                JObject resObj = JObject.Parse(jsonRes);
                var contentToken = resObj["choices"]?[0]?["message"]?["content"];

                if (contentToken == null)
                {
                    MessageBox.Show("A IA não retornou o texto esperado.", "Aviso");
                    return;
                }

                string iaContent = contentToken.ToString().Trim();
                if (iaContent.StartsWith("```json")) iaContent = iaContent.Replace("```json", "");
                if (iaContent.StartsWith("```")) iaContent = iaContent.Replace("```", "");
                iaContent = iaContent.Trim();

                JObject dados;
                try { dados = JObject.Parse(iaContent); }
                catch (Exception) { MessageBox.Show("A IA não mandou um JSON válido.", "Erro de Formatação"); return; }

                Application.Current.Dispatcher.Invoke(() => {
                    SlidesCarrossel.Clear();
                    string titulo = dados["titulo_capa"]?.ToString() ?? "TÍTULO NÃO GERADO";
                    string subtitulo = dados["subtitulo_capa"]?.ToString() ?? "";
                    string keywordCapa = dados["keyword_capa"]?.ToString() ?? "dark cinematic background";

                    SlidesCarrossel.Add(new SlideCuriosidade { EhCapa = true, Texto = titulo, Subtitulo = subtitulo, Keyword = keywordCapa });

                    var arraySlides = dados["slides"] as JArray;
                    if (arraySlides != null)
                    {
                        foreach (var s in arraySlides)
                        {
                            string fato = s["fato"]?.ToString() ?? "Texto não gerado";
                            string keyword = s["keyword"]?.ToString() ?? "dark cinematic background";
                            SlidesCarrossel.Add(new SlideCuriosidade { EhCapa = false, Texto = fato, Keyword = keyword });
                        }
                    }
                    TxtLegendaGlobal.Text = dados["legenda"]?.ToString() ?? "";
                });

                int total = SlidesCarrossel.Count;

                for (int i = 0; i < total; i++)
                {
                    var slide = SlidesCarrossel[i];


                    Application.Current.Dispatcher.Invoke(() => {
                        TxtProgressoStatus.Text = $"Pintando slide {i + 1} de {total}... 🖼️ (Isso pode levar alguns segundos)";
                        BarraProgresso.Value = 30 + ((double)i / total * 65);
                    });


                    slide.ImagemFundoBytes = await FetchImageForSlide(slide.Keyword, fonteSelecionada);


                    if (i < total - 1)
                    {
                        await Task.Delay(3000);
                    }
                }


                Application.Current.Dispatcher.Invoke(() => {
                    BarraProgresso.Value = 100;
                    TxtProgressoStatus.Text = "Tudo pronto! ✨ Abrindo editor...";

                    if (SlidesCarrossel.Count > 0)
                    {
                        GridEditorTrabalho.Visibility = Visibility.Visible;
                        ListaSlidesEditor.SelectedIndex = 0;
                        ListaSlidesEditor_SelectionChanged(null, null);
                    }
                });

                await Task.Delay(800);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocorreu um erro: {ex.Message}");
            }
            finally
            {
                OverlayProgresso.Visibility = Visibility.Collapsed;
                BtnGerarRoteiroBase.IsEnabled = true;
            }
        }
        
        private bool _isUpdatingFromCode = false;
        private void ListaSlidesEditor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListaSlidesEditor.SelectedItem is SlideCuriosidade slide)
            {
                _isUpdatingFromCode = true;

                LblTituloPainel.Text = $"EDITANDO: {slide.NomeSlide.ToUpper()}";
                EditorTextoSlide.Text = slide.Texto;
                EditorKeywordSlide.Text = slide.Keyword;

                if (slide.EhCapa)
                {
                    PainelSubtitulo.Visibility = Visibility.Visible;
                    EditorSubtituloSlide.Text = slide.Subtitulo;
                    PreviewSubtitulo.Visibility = Visibility.Visible;
                    PreviewSubtitulo.Text = slide.Subtitulo;
                }
                else
                {
                    PainelSubtitulo.Visibility = Visibility.Collapsed;
                    PreviewSubtitulo.Visibility = Visibility.Collapsed;
                }

                PreviewTextoPrincipal.Text = slide.Texto.ToUpper();
                AtualizarImagemNoPreview(slide.ImagemFundoBytes);

                _isUpdatingFromCode = false;
            }
        }

        private void EditorCampos_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingFromCode || ListaSlidesEditor.SelectedItem is not SlideCuriosidade slide) return;

            slide.Texto = EditorTextoSlide.Text;
            PreviewTextoPrincipal.Text = EditorTextoSlide.Text.ToUpper();

            if (slide.EhCapa)
            {
                slide.Subtitulo = EditorSubtituloSlide.Text;
                PreviewSubtitulo.Text = EditorSubtituloSlide.Text.ToUpper();
            }

            slide.Keyword = EditorKeywordSlide.Text;
        }

        private async void BtnSugerirKeyword_Click(object sender, RoutedEventArgs e)
        {
            if (ListaSlidesEditor.SelectedItem is not SlideCuriosidade slide) return;
            BtnSugerirKeyword.IsEnabled = false; BtnSugerirKeyword.Content = "...";

            try
            {
                string regra = ((ComboBoxItem)CmbFonteImagem.SelectedItem).Content.ToString().Contains("IA")
                    ? "Retorne UM prompt curto e hiper-detalhado em inglês focado no cenário, sem texto. Ex: 'dark cinematic misty forest at midnight 8k'"
                    : "Retorne UMA palavra chave ou frase literal em inglês focada no cenário ou objeto.";

                string prompt = $"Leia este fato e {regra}. Responda APENAS com o prompt em inglês, sem aspas.\nFato: {slide.Texto}";
                var requestBody = new { model = "llama-3.3-70b-versatile", messages = new[] { new { role = "user", content = prompt } } };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
                request.Headers.Add("Authorization", $"Bearer {_groqKey}");
                request.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(requestBody), System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    JObject res = JObject.Parse(await response.Content.ReadAsStringAsync());
                    string novaKeyword = res["choices"][0]["message"]["content"].ToString().Replace("\"", "").Trim();
                    EditorKeywordSlide.Text = novaKeyword;
                }
            }
            catch { }
            finally { BtnSugerirKeyword.IsEnabled = true; BtnSugerirKeyword.Content = "🧠 IA: Sugerir Nova Keyword"; }
        }

        private async void BtnTrocarFundo_Click(object sender, RoutedEventArgs e)
        {
            if (ListaSlidesEditor.SelectedItem is not SlideCuriosidade slide) return;
            BtnTrocarFundo.IsEnabled = false; BtnTrocarFundo.Content = "Buscando... ⏳";

            string fonteSelecionada = ((ComboBoxItem)CmbFonteImagem.SelectedItem).Content.ToString();
            byte[] novaImg = await FetchImageForSlide(slide.Keyword, fonteSelecionada);

            if (novaImg != null)
            {
                slide.ImagemFundoBytes = novaImg;
                slide.PosicaoX = 0;
                slide.PosicaoY = 0;
                AtualizarImagemNoPreview(novaImg);
            }

            BtnTrocarFundo.IsEnabled = true; BtnTrocarFundo.Content = "🔄 Baixar Fundo (Keyword da IA)";
        }

        private void BtnUploadFundoLocal_Click(object sender, RoutedEventArgs e)
        {
            if (ListaSlidesEditor.SelectedItem is not SlideCuriosidade slide) return;

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Selecione a Imagem de Fundo",
                Filter = "Imagens (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|Todos os arquivos (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    byte[] novaImg = System.IO.File.ReadAllBytes(openFileDialog.FileName);
                    if (novaImg != null && novaImg.Length > 0)
                    {
                        slide.ImagemFundoBytes = novaImg;
                        slide.PosicaoX = 0;
                        slide.PosicaoY = 0;
                        AtualizarImagemNoPreview(novaImg);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao carregar a imagem: " + ex.Message);
                }
            }
        }

        // ==========================================
        // TEXT REFINEMENT (CONTEXTUALIZADO)
        // ==========================================
        private async void BtnRefinarTexto_Click(object sender, RoutedEventArgs e)
        {
            if (ListaSlidesEditor.SelectedItem is not SlideCuriosidade slide) return;

            string textoAtual = EditorTextoSlide.Text;
            string feedback = TxtFeedbackIA.Text.Trim();
            string temaGeral = TxtTemaCuriosidade.Text.Trim();
            string tipoSlide = slide.EhCapa ? "Capa do Carrossel" : "Slide de Conteúdo (Meio da história)";

            if (string.IsNullOrWhiteSpace(textoAtual) || string.IsNullOrWhiteSpace(feedback))
            {
                MessageBox.Show("Você precisa ter um texto no slide e digitar o que deseja alterar!", "Atenção");
                return;
            }

            BtnRefinarTexto.Content = "Pensando...";
            BtnRefinarTexto.IsEnabled = false;

            string prompt = $@"Você é um Diretor de Arte e Copywriter ajustando um roteiro de Instagram.
TEMA DO CARROSSEL: {temaGeral}
TIPO DE SLIDE: {tipoSlide}

TEXTO ATUAL DO SLIDE:
{textoAtual}

INSTRUÇÃO DE CORREÇÃO:
{feedback}

REGRAS:
1. Reescreva o 'TEXTO ATUAL' aplicando a instrução acima.
2. Mantenha o texto do tamanho ideal para carrossel (30 a 45 palavras se for slide, curto se for capa).
3. Retorne APENAS o novo texto final pronto, sem aspas, sem saudações.";

            try
            {
                var requestBody = new { model = "llama-3.3-70b-versatile", messages = new[] { new { role = "user", content = prompt } } };
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
                request.Headers.Add("Authorization", $"Bearer {_groqKey}");
                request.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(requestBody), System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    JObject res = JObject.Parse(await response.Content.ReadAsStringAsync());
                    string novoTexto = res["choices"][0]["message"]["content"].ToString().Replace("\"", "").Trim();
                    EditorTextoSlide.Text = novoTexto;
                    TxtFeedbackIA.Text = "";
                }
                else { MessageBox.Show("Erro na IA ao tentar refinar o texto."); }
            }
            catch (Exception ex) { MessageBox.Show("Erro de conexão: " + ex.Message); }
            finally { BtnRefinarTexto.Content = "Refazer 🪄"; BtnRefinarTexto.IsEnabled = true; }
        }

        // ==========================================
        // BUSCA BING (COM CACHE)
        // ==========================================
        private async void BtnBuscaManualImagem_Click(object sender, RoutedEventArgs e)
        {
            if (ListaSlidesEditor.SelectedItem is not SlideCuriosidade slide) return;

            string busca = TxtBuscaManualImagem.Text.Trim();
            if (string.IsNullOrWhiteSpace(busca))
            {
                MessageBox.Show("Digite o nome de alguém ou algo para buscar na internet!", "Atenção");
                return;
            }

            BtnBuscaManualImagem.Content = "Buscando Web...";
            BtnBuscaManualImagem.IsEnabled = false;

            try
            {
                byte[] novaImg = await BuscarImagemNaInternet(busca);
                if (novaImg != null && novaImg.Length > 0)
                {
                    slide.ImagemFundoBytes = novaImg;
                    slide.PosicaoX = 0;
                    slide.PosicaoY = 0;
                    AtualizarImagemNoPreview(novaImg);
                }
                else { MessageBox.Show("Não consegui puxar essa imagem ou chegamos no fim da lista."); }
            }
            catch (Exception ex) { MessageBox.Show("Erro ao baixar a imagem: " + ex.Message); }
            finally { BtnBuscaManualImagem.Content = "Buscar 🔍"; BtnBuscaManualImagem.IsEnabled = true; }
        }

        private async Task<byte[]> BuscarImagemNaInternet(string query)
        {
            try
            {
                string chave = query.ToLower().Trim();

                if (!_cacheBuscaBing.ContainsKey(chave))
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.bing.com/images/search?q={Uri.EscapeDataString(query)}");
                    request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                    var response = await httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        string html = await response.Content.ReadAsStringAsync();
                        var matches = Regex.Matches(html, @"murl&quot;:&quot;(http[^&]+)&quot;");

                        var urls = new List<string>();
                        foreach (Match match in matches) urls.Add(match.Groups[1].Value);

                        if (urls.Count > 0) { _cacheBuscaBing[chave] = urls; _indiceBuscaBing[chave] = 0; }
                        else return null;
                    }
                    else return null;
                }

                var listaUrls = _cacheBuscaBing[chave];
                int indiceAtual = _indiceBuscaBing[chave];
                string urlDaImagem = listaUrls[indiceAtual];

                _indiceBuscaBing[chave] = (indiceAtual + 1) % listaUrls.Count;
                return await httpClient.GetByteArrayAsync(urlDaImagem);
            }
            catch { return null; }
        }


        private async Task<byte[]> FetchImageForSlide(string query, string fonte, int tentativas = 3)
        {
            if (string.IsNullOrWhiteSpace(query)) query = "dark cinematic epic background";

            for (int i = 0; i < tentativas; i++)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));

                try
                {
                    if (fonte.Contains("Pexels"))
                    {
                        var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(query)}&per_page=15&orientation=portrait");
                        req.Headers.Add("Authorization", _pexelsApiKey);

                        var res = await httpClient.SendAsync(req, cts.Token);


                        if (!res.IsSuccessStatusCode)
                        {
                            string erro = await res.Content.ReadAsStringAsync();
                            Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Erro Pexels ({res.StatusCode}):\n{erro}", "Diagnóstico"));
                            break; 
                        }

                        var json = JObject.Parse(await res.Content.ReadAsStringAsync());
                        var photos = json["photos"] as JArray;
                        if (photos != null && photos.Count > 0)
                        {
                            string url = photos[Random.Shared.Next(photos.Count)]["src"]?["portrait"]?.ToString();
                            return await httpClient.GetByteArrayAsync(url, cts.Token);
                        }
                    }
                    else if (fonte.Contains("Unsplash"))
                    {
                        var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.unsplash.com/search/photos?query={Uri.EscapeDataString(query)}&per_page=15&orientation=portrait");
                        req.Headers.Add("Authorization", $"Client-ID {_unsplashApiKey}");

                        var res = await httpClient.SendAsync(req, cts.Token);

                        
                        if (!res.IsSuccessStatusCode)
                        {
                            string erro = await res.Content.ReadAsStringAsync();
                            Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Erro Unsplash ({res.StatusCode}):\n{erro}", "Diagnóstico"));
                            break;
                        }

                        var json = JObject.Parse(await res.Content.ReadAsStringAsync());
                        var results = json["results"] as JArray;
                        if (results != null && results.Count > 0)
                        {
                            string url = results[Random.Shared.Next(results.Count)]["urls"]?["regular"]?.ToString();
                            return await httpClient.GetByteArrayAsync(url, cts.Token);
                        }
                    }
                    else if (fonte.Contains("IA"))
                    {
                        string hashUnico = Guid.NewGuid().ToString("N").Substring(0, 6);
                        string promptPremium = query + $", highly detailed photography, cinematic lighting, 8k, empty background, unique {hashUnico}";
                        string urlSegura = Uri.EscapeDataString(promptPremium);
                        long seed = Random.Shared.Next(1, 9999999);

                        string url = $"https://image.pollinations.ai/prompt/{urlSegura}?width=1080&height=1350&nologo=true&seed={seed}";

                        var res = await httpClient.GetAsync(url, cts.Token);
                        if (res.IsSuccessStatusCode)
                        {
                            byte[] bytes = await res.Content.ReadAsByteArrayAsync(cts.Token);
                            if (bytes != null && bytes.Length > 1000) return bytes;
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"A IA de imagens falhou com erro: {res.StatusCode}", "Diagnóstico"));
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Erro (Tentativa {i + 1}): {ex.Message}");
                }

                if (i < tentativas - 1) await Task.Delay(2000);
            }


            using (var fallbackImg = new SixLabors.ImageSharp.Image<Rgba32>(1080, 1350))
            {
                fallbackImg.Mutate(x => x.Fill(SLColor.ParseHex("#1E1E1E")));
                using (var ms = new MemoryStream())
                {
                    fallbackImg.Save(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
                    return ms.ToArray();
                }
            }
        }
        private void AtualizarImagemNoPreview(byte[] imgBytes)
        {
            AvisoImagemFixa.Visibility = Visibility.Collapsed;
            if (imgBytes == null || imgBytes.Length == 0) return;
            try
            {
                var image = new BitmapImage();
                using (var mem = new MemoryStream(imgBytes))
                {
                    mem.Position = 0;
                    image.BeginInit();
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = mem;
                    image.EndInit();
                }
                image.Freeze();
                PreviewImagemFundo.Source = image;

                double canvasW = 1080.0; double canvasH = 1350.0;
                double imgW = image.PixelWidth; double imgH = image.PixelHeight;

                double scale = Math.Max(canvasW / imgW, canvasH / imgH);
                PreviewImagemFundo.Width = imgW * scale;
                PreviewImagemFundo.Height = imgH * scale;

                _limiteMinX = canvasW - PreviewImagemFundo.Width;
                _limiteMinY = canvasH - PreviewImagemFundo.Height;

                if (Math.Abs(_limiteMinX) < 1 && Math.Abs(_limiteMinY) < 1)
                {
                    AvisoImagemFixa.Visibility = Visibility.Visible;
                }

                if (ListaSlidesEditor.SelectedItem is SlideCuriosidade slide)
                {
                    if (slide.PosicaoX == 0 && slide.PosicaoY == 0)
                    {
                        slide.PosicaoX = _limiteMinX / 2.0;
                        slide.PosicaoY = _limiteMinY / 2.0;
                    }
                    ImageTranslate.X = slide.PosicaoX;
                    ImageTranslate.Y = slide.PosicaoY;
                }
            }
            catch { }
        }

        // ==========================================
        // LÓGICA DE CLIQUE E ARRASTE NO PREVIEW
        // ==========================================
        private void PreviewCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (AvisoImagemFixa.Visibility == Visibility.Visible) return;
            _isDragging = true;
            _clickPosition = e.GetPosition((UIElement)sender);
            ((UIElement)sender).CaptureMouse();
        }

        private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || ListaSlidesEditor.SelectedItem is not SlideCuriosidade slide) return;

            Point currentPosition = e.GetPosition((UIElement)sender);
            double moveX = currentPosition.X - _clickPosition.X;
            double moveY = currentPosition.Y - _clickPosition.Y;

            double newX = slide.PosicaoX + moveX;
            double newY = slide.PosicaoY + moveY;

            newX = Math.Max(_limiteMinX, Math.Min(0, newX));
            newY = Math.Max(_limiteMinY, Math.Min(0, newY));

            ImageTranslate.X = newX;
            ImageTranslate.Y = newY;
        }

        private void PreviewCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;
            ((UIElement)sender).ReleaseMouseCapture();

            if (ListaSlidesEditor.SelectedItem is SlideCuriosidade slide)
            {
                slide.PosicaoX = ImageTranslate.X;
                slide.PosicaoY = ImageTranslate.Y;
            }
        }

        private void PreviewCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDragging) PreviewCanvas_MouseUp(sender, null);
        }


        private async void BtnFinalizarCarrossel_Click(object sender, RoutedEventArgs e)
        {
            if (SlidesCarrossel.Count == 0) return;
            BtnFinalizarCarrossel.Content = "Assando Imagens e Enviando... 🍳"; BtnFinalizarCarrossel.IsEnabled = false;

            try
            {
                string paginaSelecionada = ((ComboBoxItem)CmbPagina.SelectedItem).Content.ToString();
                string nomeLogo = paginaSelecionada == "Explicando em Imagens" ? "logotipo.png" : "logotipo02.png";
                string pathLogo = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "img", nomeLogo);

                var carrosselBytes = new List<byte[]>();
                int total = Math.Min(SlidesCarrossel.Count, 10);

                for (int i = 0; i < total; i++)
                {
                    BtnFinalizarCarrossel.Content = $"Assando Slide {i + 1}/{total}...";
                    var slide = SlidesCarrossel[i];
                    byte[] imgFinal = await Task.Run(() => ProcessarImagemComImageSharp(slide, i, pathLogo));
                    carrosselBytes.Add(imgFinal);
                }

                BtnFinalizarCarrossel.Content = "Enviando pro Telegram... 🚀";
                var album = new List<IAlbumInputMedia>();
                var streams = new List<MemoryStream>();

                for (int i = 0; i < carrosselBytes.Count; i++)
                {
                    var ms = new MemoryStream(carrosselBytes[i]);
                    streams.Add(ms);
                    album.Add(new InputMediaPhoto(InputFile.FromStream(ms, $"s{i}.jpg")));
                }

                await botClient.SendMediaGroupAsync(_chatId, album);

                string legenda = TxtLegendaGlobal.Text;
                if (!string.IsNullOrWhiteSpace(legenda))
                {
                    string textoFinal = legenda.Length > 4000 ? legenda.Substring(0, 4000) + "..." : legenda;
                    await botClient.SendTextMessageAsync(_chatId, textoFinal);
                }

                foreach (var s in streams) s.Dispose();

                MessageBox.Show($"Design System Finalizado e Enviado com Sucesso!");
            }
            catch (Exception ex) { MessageBox.Show("Erro ao Assar: " + ex.Message); }
            finally { BtnFinalizarCarrossel.Content = "2. Assar Imagens e Enviar 🚀"; BtnFinalizarCarrossel.IsEnabled = true; }
        }

        private byte[] ProcessarImagemComImageSharp(SlideCuriosidade slide, int index, string pathLogo)
        {
            SLImage rawImg;
            if (slide.ImagemFundoBytes != null && slide.ImagemFundoBytes.Length > 0)
            {
                rawImg = SLImage.Load<Rgba32>(slide.ImagemFundoBytes);

                double canvasW = 1080.0; double canvasH = 1350.0;
                double scale = Math.Max(canvasW / rawImg.Width, canvasH / rawImg.Height);

                int newW = (int)Math.Round(rawImg.Width * scale);
                int newH = (int)Math.Round(rawImg.Height * scale);

                rawImg.Mutate(x => x.Resize(newW, newH));

                int cropX = (int)Math.Round(Math.Abs(slide.PosicaoX));
                int cropY = (int)Math.Round(Math.Abs(slide.PosicaoY));

                rawImg.Mutate(x => x.Crop(new SixLabors.ImageSharp.Rectangle(cropX, cropY, 1080, 1350)));
            }
            else
            {
                rawImg = new SixLabors.ImageSharp.Image<Rgba32>(1080, 1350);
                rawImg.Mutate(x => x.Fill(SLColor.ParseHex("#1a1a2e")));
            }

            using (rawImg)
            {
                const float DEGRADE_INICIO = 0.50f;
                const float DEGRADE_BLOCO_PRETO = 0.75f;

                var dBrush = new SixLabors.ImageSharp.Drawing.Processing.LinearGradientBrush(
                    new System.Numerics.Vector2(0, 0),
                    new System.Numerics.Vector2(0, 1350),
                    GradientRepetitionMode.None,
                    new ColorStop(DEGRADE_INICIO, SLColor.Transparent),
                    new ColorStop(DEGRADE_BLOCO_PRETO, SLColor.Black),
                    new ColorStop(1.0f, SLColor.Black)
                );

                rawImg.Mutate(x => x.Fill(dBrush));

                var linhaBrush = new SixLabors.ImageSharp.Drawing.Processing.SolidBrush(SLColor.White);
                int linhaY = 930; int espessura = 3;
                rawImg.Mutate(x => x.Fill(linhaBrush, new SixLabors.ImageSharp.Rectangle(566, linhaY, 384, espessura)));
                rawImg.Mutate(x => x.Fill(linhaBrush, new SixLabors.ImageSharp.Rectangle(130, linhaY, 384, espessura)));

                bool temLogo = System.IO.File.Exists(pathLogo);
                if (temLogo)
                {
                    using (var logo = SLImage.Load(pathLogo))
                    {
                        logo.Mutate(x => x.Resize(113, 113));
                        rawImg.Mutate(x => x.DrawImage(logo, new SixLabors.ImageSharp.Point(484, 872), 1f));
                    }
                }

                FontCollection collection = new FontCollection();
                string caminhoFonte = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts", "Gordita-Black.ttf");
                SixLabors.Fonts.FontFamily family = System.IO.File.Exists(caminhoFonte) ? collection.Add(caminhoFonte) : SixLabors.Fonts.SystemFonts.Families.FirstOrDefault(f => f.Name.Contains("Arial"));

                int yTextoStart = 1010;
                int yLimiteRodape = 1260;

                string txtPrincipalUpper = slide.Texto?.ToUpper() ?? "";
                string txtSecundarioUpper = slide.Subtitulo?.ToUpper() ?? "";

                float fontSizeP = slide.EhCapa ? 50f : 42f;
                SixLabors.Fonts.Font fontP; RichTextOptions optP; SixLabors.Fonts.FontRectangle boundsP;

                float alturaMaximaP = slide.EhCapa && !string.IsNullOrEmpty(txtSecundarioUpper) ? (yLimiteRodape - yTextoStart) * 0.55f : (yLimiteRodape - yTextoStart);

                while (true)
                {
                    fontP = family.CreateFont(fontSizeP, SixLabors.Fonts.FontStyle.Bold);
                    optP = new RichTextOptions(fontP)
                    {
                        Origin = new System.Numerics.Vector2(540, yTextoStart),
                        WrappingLength = 994,
                        HorizontalAlignment = SixLabors.Fonts.HorizontalAlignment.Center,
                        TextAlignment = SixLabors.Fonts.TextAlignment.Center,
                        VerticalAlignment = SixLabors.Fonts.VerticalAlignment.Top
                    };
                    boundsP = TextMeasurer.MeasureBounds(txtPrincipalUpper, optP);
                    if (boundsP.Height <= alturaMaximaP || fontSizeP <= 20f) break;
                    fontSizeP -= 2f;
                }

                if (slide.EhCapa)
                {
                    rawImg.Mutate(x => x.DrawText(optP, txtPrincipalUpper, SLColor.White));
                    if (!string.IsNullOrEmpty(txtSecundarioUpper))
                    {
                        float ySubtitulo = Math.Max(yTextoStart + 30f, boundsP.Bottom + 15f);
                        float fontSizeS = 40f; SixLabors.Fonts.Font fontS; RichTextOptions optS;

                        while (true)
                        {
                            fontS = family.CreateFont(fontSizeS, SixLabors.Fonts.FontStyle.Bold);
                            optS = new RichTextOptions(fontS)
                            {
                                Origin = new System.Numerics.Vector2(540, ySubtitulo),
                                WrappingLength = 994,
                                HorizontalAlignment = SixLabors.Fonts.HorizontalAlignment.Center,
                                TextAlignment = SixLabors.Fonts.TextAlignment.Center,
                                VerticalAlignment = SixLabors.Fonts.VerticalAlignment.Top
                            };
                            var boundsS = TextMeasurer.MeasureBounds(txtSecundarioUpper, optS);
                            if (boundsS.Bottom <= yLimiteRodape || fontSizeS <= 18f) break;
                            fontSizeS -= 2f;
                        }
                        rawImg.Mutate(x => x.DrawText(optS, txtSecundarioUpper, SLColor.White));
                    }
                }
                else
                {
                    rawImg.Mutate(x => x.DrawText(optP, txtPrincipalUpper, SLColor.White));
                }

                var fontesDeSocorro = new List<SixLabors.Fonts.FontFamily>();
                if (SixLabors.Fonts.SystemFonts.TryGet("Segoe UI Emoji", out var emojiFont)) fontesDeSocorro.Add(emojiFont);
                else if (SixLabors.Fonts.SystemFonts.TryGet("Segoe UI Symbol", out var symbolFont)) fontesDeSocorro.Add(symbolFont);

                var optR = new RichTextOptions(family.CreateFont(30, SixLabors.Fonts.FontStyle.Bold))
                {
                    Origin = new System.Numerics.Vector2(540, 1300),
                    HorizontalAlignment = SixLabors.Fonts.HorizontalAlignment.Center,
                    VerticalAlignment = SixLabors.Fonts.VerticalAlignment.Bottom,
                    FallbackFontFamilies = fontesDeSocorro
                };

                rawImg.Mutate(x => x.DrawText(optR, "ARRASTE ⇒", SLColor.White));

                using (var ms = new MemoryStream()) { rawImg.Save(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder()); return ms.ToArray(); }
            }
        }
    }
}