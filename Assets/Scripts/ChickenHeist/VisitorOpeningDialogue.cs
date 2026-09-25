// New voice IDs intentionally cannot resolve to the previous Elias/Lia recordings.
public static class VisitorOpeningDialogue
{
    public static readonly string[] Speakers={"","","Elias","Visitante","Elias","Visitante","Visitante","Elias","Visitante","","Elias","Visitante","Elias","Visitante","","Elias","Visitante","Elias","Visitante","","Visitante","Elias","Visitante","Elias","Visitante","Visitante","Elias","Visitante","","",""};
    public static readonly string[] Text={
        "","","Quem é?","Vim falar de trabalho. Não vou demorar.","Trabalho? A essa hora?","Trabalho que se faz de dia não paga o que eu pago.","Vi o aviso da luz no seu portão. E a parcela do sítio vence semana que vem.","Se veio cobrar, pode ir embora.","Não vim cobrar. Vim comprar.","","Comprar o quê? Não tenho nada pra vender.","Galinhas. Vivas. Você traz, eu pago na hora.","Essas fazendas não são minhas.","Eu sei de quem são.","","Quem tirou essas fotos?","Alguém que não pode mais voltar lá.","E agora você quer que eu vá no lugar dele.","Quero que entre e saia sem acordar ninguém.","","Não paga a dívida inteira. Mas segura o banco por mais um mês.","Por que eu?","Você tem caminhonete, conhece as estradas de terra e ninguém estranha mais um sitiante voltando tarde.","E esse tablet?","Fica com você.","As fazendas, o preço de cada ave e o ponto de entrega estão aí dentro.","Eu não disse que aceito.","Nem precisa. Olha primeiro. Depois decide.","","",""};
    public static readonly float[] Durations={6f,4.6f,2.4f,3.8f,2.7f,5.6f,7.4f,3.5f,3.1f,2.7f,4.2f,4.2f,3.1f,3.1f,2.6f,2.7f,3.8f,4.9f,4.2f,2.8f,6,2.4f,7.1f,2.4f,2.4f,6.7f,3.1f,3.5f,6.5f,8.5f,4.8f};
    public static string VoiceFile(int line)=>string.IsNullOrEmpty(Text[line])?"":"visitor_opening_"+line.ToString("00")+"_"+(Speakers[line]=="Elias"?"elias":"visitante");
    public static readonly string[] Files=CreateFiles();
    static string[] CreateFiles(){var files=new string[Text.Length];for(int i=0;i<files.Length;i++)files[i]=VoiceFile(i);return files;}
}
