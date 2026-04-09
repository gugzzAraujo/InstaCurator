using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstaCurator.Models
{
    public class SlideCuriosidade
    {
        public bool EhCapa { get; set; }
        public string NomeSlide => EhCapa ? "Capa" : "Slide de Fato";

        public string Texto { get; set; }
        public string Subtitulo { get; set; }
        public string Keyword { get; set; }

        public byte[] ImagemFundoBytes { get; set; }

        // NOVAS PROPRIEDADES PARA O ARRASTE DO FUNDO
        public double PosicaoX { get; set; } = 0;
        public double PosicaoY { get; set; } = 0;
    }
}
