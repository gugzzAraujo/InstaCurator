using System.Collections.Generic;

namespace InstaCurator.Models
{
    public class PostInstagram
    {
        public string PaginaRival { get; set; }
        public string Legenda { get; set; }
        public int QuantidadeLikes { get; set; }

        public List<string> CaminhosDasImagens { get; set; }

        public string LinkPost { get; set; }

        public PostInstagram()
        {
            CaminhosDasImagens = new List<string>();
        }
    }
}