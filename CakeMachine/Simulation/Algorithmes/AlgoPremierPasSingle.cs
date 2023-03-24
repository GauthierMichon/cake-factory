using CakeMachine.Fabrication.ContexteProduction;
using CakeMachine.Fabrication.Elements;
using System.Runtime.CompilerServices;

namespace CakeMachine.Simulation.Algorithmes;

internal class AlgoPremierPasSingle : Algorithme
{
    public override bool SupportsSync => true;

    public override bool SupportsAsync => true;

    public override IEnumerable<GâteauEmballé> Produire(Usine usine, CancellationToken token)
    {
        var postePréparation = usine.Préparateurs.Single();
        var posteCuisson = usine.Fours.Single();
        var posteEmballage = usine.Emballeuses.Single();

        while (!token.IsCancellationRequested)
        {
            var plats = usine.StockInfiniPlats.Take(2);

            var gâteauxCrus = plats
                .Select(postePréparation.Préparer)
                .ToArray();


            var gâteauxCuits = posteCuisson.Cuire(gâteauxCrus);


            var gâteauxEmballés = gâteauxCuits
                .Select(posteEmballage.Emballer);

            foreach (var gâteauEmballé in gâteauxEmballés)
                yield return gâteauEmballé;
        }
    }

    public override async IAsyncEnumerable<GâteauEmballé> ProduireAsync(
        Usine usine,
        [EnumeratorCancellation] CancellationToken token)
    {
        var postePréparation = usine.Préparateurs.Single();
        var posteCuisson = usine.Fours.Single();
        var posteEmballage = usine.Emballeuses.Single();

        while (!token.IsCancellationRequested)
        {
            var plat = usine.StockInfiniPlats.First();

            var gâteauCru = await postePréparation.PréparerAsync(plat);
            var gâteauCuit = (await posteCuisson.CuireAsync(gâteauCru)).Single();
            var gâteauEmballé = await posteEmballage.EmballerAsync(gâteauCuit);

            yield return gâteauEmballé;
        }
    }
}