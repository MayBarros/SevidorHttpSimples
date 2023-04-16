using System.Net;
using System.Net.Sockets;
using System.Text;
using System;
using System.IO;
using  System.Threading.Tasks;

class ServidorHttp
{
    private TcpListener Controlador { get; set; } // responsavél por escutar as portas esperando qualquer tipo de solicitação tcp
    private int Porta { get; set; } // número da porta que será escutada
    private int QtdeRequest { get; set; } // contador para vê as requisições 

    //construindo conteúdo de retorno no browser html
    public string HtmlExemplo { get; set; }


    //construtor

    public ServidorHttp(int porta = 8080)
    {
        this.Porta = porta; // se eu chamar o construtor sem informar a porta ela irá retornar a 8080 devido ao parâmetro colocado em cima
        this.CriarHtmlExemplo(); // chamando o metodo criado
        
        try
        {
            this.Controlador = new TcpListener(IPAddress.Parse("127.0.0.1"), this.Porta); // criando um novo objeto tcplistner que vai escutar no IP 127.0.0.1(IP local da maq. na porta informada)
            this.Controlador.Start();  // inicia a escuta da porta 8080 Ip 127.0.0.1

            //algumas msg para o usuário
            Console.WriteLine($"Serrvidor HTTP está rodando na porta {this.Porta}.");
            Console.WriteLine($"para acerssar, digite no navehador: http://localhost:{this.Porta}.");

            // temos que chamar o metodo aguardarRequest aqui no construtor pq qdo um objeto tipo http for criado ele estará pronto para atender as conexões
            Task servidorHttpTask = Task.Run(() => AguardarRequests());
            servidorHttpTask.GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Console.WriteLine($" Erro ao iniciar servidor na porta {this.Porta}:\n{e.Message}");
        }
    
    }

    // Metodo que vai agaurdar novas requisições
    //cada requisição recebida vai criar um objeto tipo socket , objeto comum para comunicação em rede 
    // esse objeto permite verificar  o conteúdo da requisição que chegou e responder ela de forma adequada

    private async Task AguardarRequests()
    {
        while(true) // loop infinito que fica aguardando uma nova requisção
        {
            Socket conexao = await this.Controlador.AcceptSocketAsync(); // quando detecta uma requisição retorno um objeto do tipo socket aqui chamado de conexao esse objeto tem os dados da requisição que permite devolver uma resposta( é o navagador do usuário)
            this.QtdeRequest++; //qdo o async aceita a requisição a qauantidade de request é incrementada, e a proxima requisição irá aguardar  outra conexao

            //vou chmar o objeto que vai processar o objeto conexao que contem os dados do request e o objeto de resposta para o usuário
            Task task = Task.Run(() => ProcessarRequest(conexao, this.QtdeRequest)); //a requisição vai para um nucleo de processamento independe e diferente do pruincipal, ajuda a otimizar o processamento. Ou seja não preciso agaurdar finalizar uma requisição para começar outra
        }
    }

    //Ainda não estamos processando a requisição que chegou. Iremos fazer um metodo para isso 
    private void ProcessarRequest(Socket conexao, int numeroRequest) // este metodo será chamado no loop acima a cada requisição feita ao nosso servidor pelo usuário
    {
        Console.WriteLine($"Processando request #{numeroRequest}...\n"); //msg inicial de processando
        if(conexao.Connected) //verificar se a conexao esta ativa, se o socket esta conectado
        {
            byte[] bytesRequisicao = new byte[1024]; // se a verificação esta ok, tenho a criação de um espaço na memória que armazena os dados da requisão na memoria.
            conexao.Receive(bytesRequisicao, bytesRequisicao.Length, 0); // primeiro parametro onde quero guardar, segundo o tamanho que quero ler e na posição de memoria 0
            string textoRequisicao = Encoding.UTF8.GetString(bytesRequisicao) //esta convertendo o bytesrequisicao no formato texto UFT8
                .Replace((char)0, ' ').Trim(); //sibstituo o caractere 0 por espaço e com o Trim elimino os espacos. Seria uma limpeza na memoria, pois o formato que vem é "poluido"
            if(textoRequisicao.Length > 0) //verificando se o texto da requisição é maior que zero
            {
                Console.WriteLine($"\n{textoRequisicao}\n");

                string[] linhas= textoRequisicao.Split("\r\n)");
                int iPrimeiroEspaco = linhas[0].IndexOf(' ');
                int iSegundoEspaco = linhas[0].LastIndexOf(' ');
                string metodohttp = linhas[0].Substring(0, iPrimeiroEspaco);
                string recursoBuscado = linhas[0].Substring(iPrimeiroEspaco + 1, iSegundoEspaco - iPrimeiroEspaco - 1);
                string versaoHttp = linhas[0].Substring(iSegundoEspaco + 1);
                iPrimeiroEspaco = linhas[1].IndexOf(' ');
                string nomehost = linhas[1].Substring(iPrimeiroEspaco +1 );


                //mudança feita para enviar a resposta ao usuário no navegador
                byte[] bytesCabecalho = null;
                var bytesConteudo = LerArquivo(recursoBuscado);
                //Encoding.UTF8.GetBytes(this.HtmlExemplo, 0, this.HtmlExemplo.Length); // transforma o texto em bytes ja que o navegador só lê sequencias de bytes
                if (bytesCabecalho.Length > 0)
                {
                    bytesCabecalho = GerarCabecalho(versaoHttp, "text/html;charset=utf-8","200", bytesConteudo.Length);
                }
                else
                {
                    bytesConteudo = Encoding.UTF8.GetBytes("<h1> Erro 404 - Arquivo Não Encontrado</h1>");
                    bytesCabecalho= GerarCabecalho(versaoHttp,"text/html;charset=utf-8", "404", bytesConteudo.Length);
                }
                // antes de finalizar a requisição enviamos o cabeçalho
                //var bytesCabecalho = GerarCabecalho(versaoHttp , "text/html;charset=utf-8" , "200", bytesConteudo.Length); //chamo o metodo GerarCabecalho qguardo resultado no bytesCabecalho - bytesConteudo é o que de conteudo estou enviando
                int bytesEnviados = conexao.Send(bytesCabecalho, bytesCabecalho.Length, 0);

                bytesEnviados += conexao.Send(bytesConteudo, bytesConteudo.Length, 0);  
                conexao.Close();

                Console.WriteLine($"\n{bytesEnviados} bytes enviados em resposta à requisição # {numeroRequest}.");
            }    
        }
        Console.WriteLine($"\nRequest {numeroRequest} finalizado."); //msg final de finalização do request

        //agora vamos chamar esse metodo dentro do metedo aguardar request apra ele processar as requsições aceitas
    }

    // ATÉ AQUI CRIAMOS UM SERVIDOR CAPAZ DE RECEBER REQUISIÇÕES AGORA VAMOS DEVOLVER AO USUÁRIO UMA RESPOSTA USANDO OS PROTOCOLOS HTTP

    public byte[] GerarCabecalho(string versaoHttp, string tipoMime, string codigoHttp, int qtdeBytes = 0 ) 
    {
        StringBuilder texto = new StringBuilder();
        texto.Append($"{versaoHttp} {codigoHttp} {Environment.NewLine}");
        texto.Append($"Server: Servidor Http Simples 1.0{Environment.NewLine}");
        texto.Append($"Content-Type: {tipoMime} {Environment.NewLine}"); // tipo de conteudo html, imagem, css
        texto.Append($"Content-Lenght: {qtdeBytes} {Environment.NewLine} {Environment.NewLine} ");
        return Encoding.UTF8.GetBytes(texto.ToString()); // converte este texto emstring e converto para bytes retornando o resultado


    }
    //construindo metodo para criar o html para retornar no browser
    private void CriarHtmlExemplo()
    {
        StringBuilder html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"pt-br\"><head><meta charset=\"UTF-8\">");
        html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.Append("<title>Página Estática</title></head><body>");
        html.Append("<h1>Página Estática</h1></body></html>");
        this.HtmlExemplo = html.ToString();

        //mudamos o metodo ProcessarRequest para o conteudo seja enviado ao usuário
    }

    public byte[] LerArquivo(string recurso)
    {
        string diretorio = " M:\\Area de trabalho\\SERVIDORHTTPSIMPLES\\www";
        string caminhoArquivo = diretorio + recurso.Replace("/", "\\");
        if(File.Exists(caminhoArquivo))
        {
            return File.ReadAllBytes(caminhoArquivo);
        }
        else return new byte[0];
    }

}