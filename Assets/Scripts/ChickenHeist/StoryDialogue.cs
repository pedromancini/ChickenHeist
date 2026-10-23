public static class StoryDialogue {
public static readonly string[] openingFiles = VisitorOpeningDialogue.Files;
public static readonly string[] openingSpeakers = VisitorOpeningDialogue.Speakers;
public static readonly string[] openingText = VisitorOpeningDialogue.Text;
// Cutscene 2, "O dia seguinte" (DeclineCinematicStage): the farm Elias raided, before dawn (0-8); its gate the same
// morning (9-12); Elias' room (13-16). No recordings: the lines play as subtitles, the mouths moving with the text.
// {N} is how many birds the player took ("Todas." when he emptied the coop).
public static readonly string[] declineFiles = new string[17];
public static readonly string[] declineSpeakers = new[]{"","Joana","Osvaldo","Joana","Osvaldo","Joana","Osvaldo","Joana","Osvaldo","","Bento","Joana","Osvaldo","Mensagem no tablet","Elias","",""};
public static readonly string[] declineText = new[]{
    "","Osvaldo... o galinheiro tá aberto.","Não arrombaram. Abriram com jeito.","Quantas?","{N}","Era a ração do mês.",
    "Marca de pneu. Careca.","Tem caminhonete velha em todo canto desse vale.","Tem. Mas pouca anda de madrugada.",
    "","Câmera no poste e lata no arame. Quem passar de noite, a gente escuta.","E se for gente daqui?","Aí a gente vai saber quem.",
    "Pagamento feito. Tem mais galinha no vale.","Era só pra segurar o banco...","",""};
public static readonly float[] declineDurations = new[]{4.2f,3.8f,3.6f,2.4f,2.8f,3.2f,3.4f,3.6f,3.8f,4.6f,5.2f,3f,3.4f,4.2f,4.4f,6.5f,3f};
static readonly string[] Counts={"Nenhuma.","Uma.","Duas.","Três.","Quatro.","Cinco.","Seis.","Sete.","Oito.","Nove.","Dez.","Onze.","Doze.","Treze.","Catorze.","Quinze."};
// the lines with the night's real count filled in
public static string[] Decline(int chickens,bool emptied)
{
    var lines=(string[])declineText.Clone();
    string count=emptied?"Todas.":chickens>=0 && chickens<Counts.Length?Counts[chickens]:chickens+".";
    for(int i=0;i<lines.Length;i++)lines[i]=lines[i].Replace("{N}",count);
    return lines;
}
}
