# 🚀 InstaCurator - Automação e Criação de Conteúdo com IA

O **InstaCurator** é uma aplicação desktop desenvolvida em **C# (WPF e .NET 9)** focada em otimizar o fluxo de trabalho de Social Medias e criadores de conteúdo. A ferramenta atua como uma "Agência IA", automatizando a curadoria de posts virais da concorrência e gerando carrosséis completos (roteiro, imagens e design) utilizando Inteligência Artificial.

## 🎯 Motivação e Impacto (A História por Trás do Código)

Este projeto nasceu de uma dor real: a necessidade de escalar a produção de conteúdo com qualidade. Atuando como Social Media, eu tinha o desafio de gerenciar e publicar **mais de 1.000 posts por mês**. 

O processo manual — que envolvia curadoria de concorrentes, criação de roteiros, busca de referências, edição de imagens e formatação final — levava de **40 a 60 minutos** por carrossel. 

Com o desenvolvimento do **InstaCurator**, consegui automatizar os maiores gargalos do fluxo de trabalho e otimizar o tempo de criação para cerca de **15 minutos** por post (um aumento de produtividade e redução de tempo na casa dos **60%**). Mais do que um simples projeto de código, essa ferramenta é a prova de como a união entre programação, automação (Web Scraping) e Inteligência Artificial pode transformar fluxos de trabalho exaustivos em processos eficientes, criativos e escaláveis.

## ✨ Funcionalidades

### 📱 1. Radar de Concorrentes (Instagram)
* **Scraping Inteligente:** Utiliza Selenium WebDriver para acessar e mapear perfis concorrentes no Instagram.
* **Extração de Mídia:** Coleta imagens, vídeos (Reels) e legendas dos últimos posts.
* **Aprovação Rápida:** Interface visual para analisar os posts e, com um clique, enviá-los diretamente para um chat/grupo do Telegram para uso posterior.

### 📌 2. Garimpo de Referências (Pinterest)
* **Busca Automatizada:** Realiza pesquisas por palavras-chave no Pinterest e extrai as imagens em alta resolução (`/originals/`).
* **Exportação Direta:** Envio rápido das referências visuais encontradas para o Telegram.

### 🧠 3. Criador de Carrosséis (IA + Mini-Canva)
* **Roteiro via IA (Groq/Llama 3):** Gera roteiros completos de 5 a 6 slides, incluindo títulos magnéticos, subtítulos, curiosidades e a legenda do post, adaptando-se a diferentes tons (Informativo, Clickbait, Sombrio, etc.).
* **Busca Dinâmica de Fundos:** A IA sugere *keywords* em inglês para buscar imagens de fundo automaticamente via APIs do Pexels, Unsplash, ou gerá-las via IA (Pollinations).
* **Editor Visual Integrado:** Um mini-editor que permite:
  * Arrastar e reposicionar a imagem de fundo no canvas.
  * Ajustar os textos gerados.
  * Refinar textos usando prompts contextuais para a IA.
  * Fazer upload de imagens locais ou buscar manualmente no Bing.
* **Renderização Automática (ImageSharp):** "Assa" a imagem final aplicando degradês escuros, tipografia (Gordita/Arial), alinhamento automático de textos, elementos visuais (setas/barras) e o logotipo da página.
* **Publicação:** Envia as imagens renderizadas em formato de álbum e a legenda gerada direto para o Telegram.

---

## 🛠️ Tecnologias Utilizadas

* **Linguagem & Framework:** C#, .NET 9.0, WPF (Windows Presentation Foundation).
* **Automação & Scraping:** `Selenium.WebDriver` (ChromeDriver).
* **Processamento de Imagem:** `SixLabors.ImageSharp` (Desenho de textos, gradientes, redimensionamento e crop).
* **APIs Integradas:**
  * **Groq API** (`llama-3.3-70b-versatile` para geração de texto e JSON).
  * **Telegram Bot API** (`Telegram.Bot` para envio de mídia e alertas).
  * **Pexels & Unsplash APIs** (Banco de imagens).
  * **RapidAPI** (Instagram Scraper).

---

## ⚙️ Como Executar o Projeto

### Pré-requisitos
* [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) instalado.
* Google Chrome instalado (o projeto gerencia instâncias do ChromeDriver automaticamente).
* Chaves de API para os serviços integrados.

### Configuração
O projeto exige um arquivo `config_equipe.json` na raiz do diretório de execução (pasta `bin/Debug/net9.0-windows/` ou junto ao executável final). Crie este arquivo com a seguinte estrutura:

```json
{
  "GroqKey": "SUA_CHAVE_AQUI",
  "BotToken": "SEU_TOKEN_DO_TELEGRAM_AQUI",
  "ChatId": 123456789,
  "UnsplashKey": "SUA_CHAVE_AQUI",
  "PexelsKey": "SUA_CHAVE_AQUI",
  "RapidApiKey": "SUA_CHAVE_AQUI"
}
```

### Rodando a Aplicação
1. Clone o repositório:
   ```bash
   git clone https://github.com/gugzzAraujo/InstaCurator.git
   ```
2. Abra a solução no Visual Studio 2022.
3. Restaure os pacotes NuGet.
4. Certifique-se de que o arquivo `config_equipe.json` está no lugar certo e com as chaves preenchidas.
5. Compile e execute o projeto (F5).

---

## 📂 Estrutura de Pastas e Recursos
* As pastas `img/` e `templates/` devem conter as imagens estáticas utilizadas pela aplicação (ex: `logo.ico`, `logotipo.png`, `logotipo02.png`).
* As fontes customizadas (ex: `Gordita-Black.ttf` ou `Arimo-Bold.ttf`) devem estar na pasta `Fonts/`.
* *Dica: Todos esses recursos estão configurados no `.csproj` para serem copiados para o diretório de saída (`PreserveNewest`).*

---

## 👨‍💻 Autor

Desenvolvido por **[Gustavo Araújo]** *Se você achou este projeto útil ou interessante, deixe uma ⭐ no repositório!*
