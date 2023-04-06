using System.Runtime.CompilerServices;
using CakeMachine.Fabrication.ContexteProduction;
using CakeMachine.Fabrication.Elements;
using CakeMachine.Fabrication.Opérations;
using CakeMachine.Utils;

namespace CakeMachine.Simulation.Algorithmes;

internal class DixParDix : Algorithme
{
    /// <inheritdoc />
    public override bool SupportsSync => true;

    /// <inheritdoc />
    public override bool SupportsAsync => true;

    /// <inheritdoc />
    public override IEnumerable<GâteauEmballé> Produire(Usine usine, CancellationToken token)
    {
        var capacitéFour = usine.OrganisationUsine.ParamètresCuisson.NombrePlaces;

        var postePréparation = usine.Préparateurs.Single();
        var posteEmballage = usine.Emballeuses.Single();
        var posteCuisson = usine.Fours.Single();

        while (!token.IsCancellationRequested)
        {
            var plats = usine.StockInfiniPlats.Take(10);

            var gâteauxCrus = plats.AsParallel().Select(postePréparation.Préparer);

            var gâteauxCuits = CuireParLots(gâteauxCrus, posteCuisson, capacitéFour);
            var gâteauxEmballés = gâteauxCuits.AsParallel().Select(posteEmballage.Emballer);

            foreach (var gâteauEmballé in gâteauxEmballés)
                yield return gâteauEmballé;
        }
    }

    private static IEnumerable<GâteauCuit> CuireParLots(IEnumerable<GâteauCru> gâteaux, Cuisson four, uint capacitéFour)
    {
        var queue = new Queue<GâteauCru>(gâteaux);

        while (queue.Any())
        {
            var gâteauxCuits = four.Cuire(queue.Dequeue(capacitéFour).ToArray());
            foreach (var gâteauCuit in gâteauxCuits)
                yield return gâteauCuit;
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<GâteauEmballé> ProduireAsync(
        Usine usine,
        [EnumeratorCancellation] CancellationToken token)
    {
        var capacitéFour = usine.OrganisationUsine.ParamètresCuisson.NombrePlaces;

        var postePréparation = usine.Préparateurs.Single();
        var posteEmballage = usine.Emballeuses.Single();
        var posteCuisson = usine.Fours.Single();

        while (!token.IsCancellationRequested)
        {

            var plats = usine.StockInfiniPlats.Take(10);

            var gâteauxMoitie1CrusTache = plats.Take(5)
                .Select(postePréparation.PréparerAsync);

            var gâteauxMoitie2CrusTache = plats.Skip(5).Take(5)
                .Select(postePréparation.PréparerAsync);

            var gâteauxMoitie1Crus = await Task.WhenAll(gâteauxMoitie1CrusTache);

            var gâteauxMoitie2Crus = await Task.WhenAll(gâteauxMoitie2CrusTache);

            var gâteauxMoitie1CuitsTache = posteCuisson.CuireAsync(gâteauxMoitie1Crus);
            var gâteauxMoitie1Cuits = (await Task.WhenAll(gâteauxMoitie1CuitsTache)).SelectMany(row => row).ToArray();

            var gâteauxMoitie2CuitsTache = posteCuisson.CuireAsync(gâteauxMoitie2Crus);
            var gâteauxMoitie2Cuits = (await Task.WhenAll(gâteauxMoitie2CuitsTache)).SelectMany(row => row).ToArray();

            var tâchesEmballage = new List<Task<GâteauEmballé>>();

            foreach (var gâteauCuit in gâteauxMoitie1Cuits)
            {
                tâchesEmballage.Add(posteEmballage.EmballerAsync(gâteauCuit));

            }

            foreach (var gâteauCuit in gâteauxMoitie2Cuits)
            {
                tâchesEmballage.Add(posteEmballage.EmballerAsync(gâteauCuit));

            }


            await foreach (var gâteauEmballé in tâchesEmballage.EnumerateCompleted().WithCancellation(token))
                yield return gâteauEmballé;

            /*var plats = usine.StockInfiniPlats.Take(10);

            var gâteauxCrus = plats
                .Select(postePréparation.PréparerAsync)
                .EnumerateCompleted();

            var gâteauxCuits = CuireParLotsAsync(gâteauxCrus, posteCuisson, capacitéFour);

            var tâchesEmballage = new List<Task<GâteauEmballé>>();
            await foreach (var gâteauCuit in gâteauxCuits.WithCancellation(token))
                tâchesEmballage.Add(posteEmballage.EmballerAsync(gâteauCuit));

            await foreach (var gâteauEmballé in tâchesEmballage.EnumerateCompleted().WithCancellation(token))
                yield return gâteauEmballé;*/
        }
    }

    private static async IAsyncEnumerable<GâteauCuit> CuireParLotsAsync(
        IAsyncEnumerable<GâteauCru> gâteaux,
        Cuisson four,
        uint capacitéFour)
    {
        var buffer = new List<GâteauCru>((int)capacitéFour);
        await foreach (var gâteauCru in gâteaux)
        {
            buffer.Add(gâteauCru);

            if (buffer.Count != capacitéFour) continue;

            var gâteauxCuits = await four.CuireAsync(buffer.ToArray());
            foreach (var gâteauCuit in gâteauxCuits)
                yield return gâteauCuit;

            buffer.Clear();
        }
    }
}