using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LogParser
{
    class Program
    {
        static void Main(string[] args)
        {
            string nullOrEmptyToExitApp = "_";

            while (!string.IsNullOrEmpty(nullOrEmptyToExitApp))
            {
                try
                {
                    var voltas = new HashSet<Volta>();
                    Console.WriteLine("Cole aqui o endereço do log:");
                    var caminho = Console.ReadLine();

                    if (!File.Exists(caminho))
                        throw new DirectoryNotFoundException(caminho);

                    foreach (var linha in File.ReadAllLines(caminho).Skip(1))
                        voltas.Add(Util.ConvertToVolta(Util.SplitLog(linha)));
                    
                    var results = voltas
                        .GroupBy(c => c.piloto)
                        .Select(r =>
                        new KeyValuePair<int, ResultadoCorrida>(
                            r.Key.numero,
                            new ResultadoCorrida()
                            {
                                posicaoChegada = null,
                                codigoPiloto = r.Key.numero,
                                nomePiloto = r.Key.nome,
                                quantidadeVoltasCompletadas = r.Count(),
                                tempoTotalProva = new TimeSpan(r.Sum(v => v.tempoVolta.Ticks)),

                                completouCorrida = r.Max(v => v.numeroVolta) == 4,

                                melhorVolta = new TimeSpan(r.Min(v => v.tempoVolta.Ticks)),
                                velocidadeMediaCorrida = new TimeSpan(r.Sum(v => v.velocidadeMediaVolta.Ticks) / r.Count()),
                                tempoChegadaAposVencedor = null
                            }
                        )
                    ).ToDictionary(i => i.Key, i => i.Value);

                    Console.WriteLine("P - COD -  Nome Piloto  - V -   Tempo Total    -    Tempo Após    -    Vel Média     - Melhor volta");

                    int posicao = 1;
                    long tempoChegadaAnterior = 0;
                    int paddingNumeroPiloto = 3;
                    int paddingNomePiloto = results.Values.Max(v => v.nomePiloto.Length);

                    foreach (var result in results.Values.OrderBy(v => v.tempoTotalProva))
                    {
                        if (result.completouCorrida)
                        {
                            result.tempoChegadaAposVencedor = new TimeSpan(tempoChegadaAnterior != 0 ? result.tempoTotalProva.Ticks - tempoChegadaAnterior : 0);
                            tempoChegadaAnterior = result.tempoTotalProva.Ticks;
                            result.posicaoChegada = posicao;
                            Console.Write(result.posicaoChegada);
                            Console.Write(" - ");
                            Console.Write(result.codigoPiloto.ToString().PadLeft(paddingNumeroPiloto));
                            Console.Write(" - ");
                            Console.Write(result.nomePiloto.PadRight(paddingNomePiloto));
                            Console.Write(" - ");
                            Console.Write(result.quantidadeVoltasCompletadas);
                            Console.Write(" - ");
                            Console.Write(result.tempoTotalProva);
                            Console.Write(" - ");
                            Console.Write(result.posicaoChegada != 1 ? result.tempoChegadaAposVencedor.Value.ToString() : string.Empty.PadRight(16));
                            Console.Write(" - ");
                            Console.Write(result.velocidadeMediaCorrida);
                            Console.Write(" - ");
                            Console.Write(result.melhorVolta);
                            Console.WriteLine();
                            posicao++;
                        }
                    }

                    Console.WriteLine($"Melhor volta da corrida: {results.Values.Min(v => v.melhorVolta)}");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine();
                    Console.WriteLine(e);
                }

                nullOrEmptyToExitApp = Console.ReadLine();
            }
        }
    }

    public class Piloto
    {
        public int numero;
        public string nome;
        
        public override bool Equals(object outro)
        {
            Piloto outroPiloto = outro as Piloto;

            if (outroPiloto == null)
                return false;

            return this.numero == outroPiloto.numero;
        }

        public override int GetHashCode()
        {
            return this.numero;
        }
    }

    public class Volta
    {
        public TimeSpan horaRegistro;
        public Piloto piloto;
        public int numeroVolta;
        public TimeSpan tempoVolta;
        public TimeSpan velocidadeMediaVolta;
    }

    public class ResultadoCorrida
    {
        public int? posicaoChegada;
        public int codigoPiloto;
        public string nomePiloto;
        public int quantidadeVoltasCompletadas;
        public TimeSpan tempoTotalProva;

        public bool completouCorrida;

        public TimeSpan melhorVolta;
        public TimeSpan velocidadeMediaCorrida;
        public TimeSpan? tempoChegadaAposVencedor;
    }

    public static class Util
    {

        public static Volta ConvertToVolta(string[] cols)
        {
            if (cols.Length != 5)
                throw new NotSupportedException($"A quantidade das colunas no log não é suportada! {cols.Length} colunas (esperado: \"5\")");

            #region horaRegistro

            TimeSpan horaRegistro;
            if (!TimeSpan.TryParse(cols[0], out horaRegistro))
                throw new NotSupportedException($"O formato da hora de registro da volta não é suportado! {cols[0]}");

            #endregion
            
            #region piloto

            var rawPiloto = cols[1].Split('–');
            if(rawPiloto.Length != 2)
                throw new NotSupportedException($"O formato dos dados do piloto não é suportado! {cols[1]}");
            
            var piloto = new Piloto();

            int numero;
            if(!int.TryParse(rawPiloto[0].Trim(), out numero))
                throw new NotSupportedException($"O número do Piloto não é suportado! {rawPiloto[0]}");
            
            piloto.numero = numero;
            piloto.nome = rawPiloto[1].Trim();

            #endregion

            #region numeroVolta

            int numeroVolta;
            if (!int.TryParse(cols[2], out numeroVolta))
                throw new NotSupportedException($"O formato do número da volta não é suportado! {cols[2]}");

            #endregion

            #region tempoVolta

            TimeSpan tempoVolta;
            var rawTempoVolta = cols[3].Split(':', '.');
            if (rawTempoVolta.Length != 3)
                throw new NotSupportedException($"O formato dos tempo de volta não é suportado! {cols[3]}");
            else
            {
                int minutoTempoVolta, segundoTempoVolta, milissegundoTempoVolta;
                if(!int.TryParse(rawTempoVolta[0], out minutoTempoVolta) ||
                   !int.TryParse(rawTempoVolta[1], out segundoTempoVolta) ||
                   !int.TryParse(rawTempoVolta[2], out milissegundoTempoVolta))
                    throw new NotSupportedException($"O formato dos tempo de volta não é suportado! {cols[3]}");
                tempoVolta = new TimeSpan(0, 0, minutoTempoVolta, segundoTempoVolta, milissegundoTempoVolta);
            }

            #endregion

            #region velocidadeMediaVolta

            TimeSpan velocidadeMediaVolta;
            var rawVelocidadeMediaVolta = cols[4].Replace(" ", string.Empty).Split(',', '.');
            if (rawVelocidadeMediaVolta.Length != 2)
                throw new NotSupportedException($"O formato da velocidade média da volta não é suportado! {cols[4]}");
            else
            {
                int segundosVelocidadeMediaVolta, milissegundosVelocidadeMediaVolta;
                if (!int.TryParse(rawVelocidadeMediaVolta[0], out segundosVelocidadeMediaVolta) ||
                   !int.TryParse(rawVelocidadeMediaVolta[1], out milissegundosVelocidadeMediaVolta))
                    throw new NotSupportedException($"O formato da velocidade média da volta não é suportado! {cols[4]}");
                velocidadeMediaVolta = new TimeSpan(0, 0, 0, segundosVelocidadeMediaVolta, milissegundosVelocidadeMediaVolta);
            }
            
            #endregion
            
            #region new Volta()

            var volta = new Volta();

            volta.horaRegistro = horaRegistro;
            volta.piloto = piloto;
            volta.numeroVolta = numeroVolta;
            volta.tempoVolta = tempoVolta;
            volta.velocidadeMediaVolta = velocidadeMediaVolta;

            #endregion

            return volta;
        }

        public static string[] SplitLog(string log)
        {
            return Regex.Replace(log.Replace("\t", " "), @"[ ]{2,}", "#").Split('#');
        }
    }
}
