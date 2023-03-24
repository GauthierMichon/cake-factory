using CakeMachine.Fabrication.ContexteProduction;
using CakeMachine.Fabrication.Elements;
using System.Runtime.CompilerServices;

namespace CakeMachine.Simulation.Algorithmes;

internal class AlgoPremierPasDeux : Algorithme
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
            var plats = usine.StockInfiniPlats.Take(5).ToArray();

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
            var plats = usine.StockInfiniPlats.Take(2);
            var gâteauxCrus = await Task.WhenAll(plats.Select(postePréparation.PréparerAsync));

            var gâteauxCuits = await posteCuisson.CuireAsync(gâteauxCrus);

            var gâteauEmballé1Task = posteEmballage.EmballerAsync(gâteauxCuits.First());
            var gâteauEmballé2Task = posteEmballage.EmballerAsync(gâteauxCuits.Last());

            var terminéeEnPremier = await Task.WhenAny(gâteauEmballé1Task, gâteauEmballé2Task);
            yield return await terminéeEnPremier;

            var terminéeEnDernier =
                gâteauEmballé1Task == terminéeEnPremier ? gâteauEmballé2Task : gâteauEmballé1Task;

            yield return await terminéeEnDernier;
        }
    }
}