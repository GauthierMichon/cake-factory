﻿using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using CakeMachine.Fabrication.ContexteProduction;
using CakeMachine.Fabrication.Elements;
using CakeMachine.Fabrication.Opérations;
using CakeMachine.Utils;

namespace CakeMachine.Simulation.Algorithmes;

internal class Usine_MICHON_FERNANDEZ : Algorithme
{
    /// <inheritdoc />
    public override bool SupportsAsync => true;

    /// <inheritdoc />
    public override void ConfigurerUsine(IConfigurationUsine builder)
    {
        builder.NombrePréparateurs = 10;
        builder.NombreFours = 12;
        builder.NombreEmballeuses = 13;
    }

    private class OrdreProduction
    {
        private readonly Usine _usine;
        private readonly CancellationToken _token;
        private readonly Ring<Emballage> _emballeuses;
        private readonly Ring<Cuisson> _fours;
        private readonly Ring<Préparation> _préparatrices;

        public OrdreProduction(Usine usine, CancellationToken token)
        {
            _usine = usine;
            _token = token;
            _emballeuses = new Ring<Emballage>(usine.Emballeuses);
            _fours = new Ring<Cuisson>(usine.Fours);
            _préparatrices = new Ring<Préparation>(usine.Préparateurs);
        }

        public async IAsyncEnumerable<GâteauEmballé> ProduireAsync()
        {
            while (!_token.IsCancellationRequested)
            {
                var gâteauxCuits = ProduireEtCuireParBains(_usine.OrganisationUsine.ParamètresCuisson.NombrePlaces, 13);

                var tâchesEmballage = new List<Task<GâteauEmballé>>(
                    _usine.OrganisationUsine.ParamètresCuisson.NombrePlaces * _usine.OrganisationUsine.NombreFours
                );

                await foreach (var gâteauCuit in gâteauxCuits.WithCancellation(_token))
                {
                    if (!gâteauCuit.EstConforme)
                    {
                        _usine.MettreAuRebut(gâteauCuit);
                    }
                    else
                    {
                        tâchesEmballage.Add(_emballeuses.Next.EmballerAsync(gâteauCuit));
                    }
                }

                await foreach (var gâteauEmballé in tâchesEmballage.EnumerateCompleted().WithCancellation(_token))
                {
                    if (!gâteauEmballé.EstConforme)
                    {
                        _usine.MettreAuRebut(gâteauEmballé);
                    }
                    else
                    {
                        yield return gâteauEmballé;
                    }
                }
            }
        }

        private async IAsyncEnumerable<GâteauCuit> ProduireEtCuireParBains(
            ushort nombrePlacesParFour,
            ushort nombreBains)
        {
            var gâteauxCrus = PréparerConformesParBainAsync(nombrePlacesParFour, nombreBains);

            var tachesCuisson = new List<Task<GâteauCuit[]>>();
            await foreach (var bainGâteauxCrus in gâteauxCrus.WithCancellation(_token))
                tachesCuisson.Add(_fours.Next.CuireAsync(bainGâteauxCrus));

            await foreach (var bainGâteauxCuits in tachesCuisson.EnumerateCompleted().WithCancellation(_token))
            {
                foreach (var gâteauCuit in bainGâteauxCuits)
                {
                    if (!gâteauCuit.EstConforme)
                    {
                        _usine.MettreAuRebut(gâteauCuit);
                    }
                    else
                    {
                        yield return gâteauCuit;
                    }
                }
            }
        }

        private async IAsyncEnumerable<GâteauCru[]> PréparerConformesParBainAsync(
            ushort gâteauxParBain, ushort bains)
        {
            var totalAPréparer = (ushort)(bains * gâteauxParBain);
            var gâteauxConformes = 0;
            var gâteauxRatés = 0;
            var gâteauxPrêts = new ConcurrentBag<GâteauCru>();

            async Task TakeNextAndSpawnChild(uint depth)
            {
                _token.ThrowIfCancellationRequested();
                // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
                while (depth >= totalAPréparer + gâteauxRatés)
                {
                    _token.ThrowIfCancellationRequested();
                    if (gâteauxConformes == totalAPréparer) return;
                    await Task.Delay(_usine.OrganisationUsine.ParamètresPréparation.TempsMin / 2, _token);
                }

                if (gâteauxConformes == totalAPréparer) return;

                var préparatrice = _préparatrices.Next;

                var child = TakeNextAndSpawnChild(depth + 1);
                await PréparerPlat(préparatrice);
                await child;
            }

            async Task PréparerPlat(Préparation préparatrice)
            {
                _token.ThrowIfCancellationRequested();

                var gateau = await préparatrice.PréparerAsync(_usine.StockInfiniPlats.First());
                if (gateau.EstConforme)
                {
                    gâteauxPrêts!.Add(gateau);
                    Interlocked.Increment(ref gâteauxConformes);
                }
                else
                {
                    _usine.MettreAuRebut(gateau);
                    Interlocked.Increment(ref gâteauxRatés);
                }

                ;
            }

            var spawner = TakeNextAndSpawnChild(0);

            var buffer = new List<GâteauCru>(gâteauxParBain);
            for (var i = 0; i < totalAPréparer; i++)
            {
                _token.ThrowIfCancellationRequested();

                GâteauCru gâteauPrêt;

                while (!gâteauxPrêts.TryTake(out gâteauPrêt!))
                {
                    _token.ThrowIfCancellationRequested();
                    await Task.Delay(_usine.OrganisationUsine.ParamètresPréparation.TempsMin / 2, _token);
                }

                buffer.Add(gâteauPrêt);

                if (buffer.Count != gâteauxParBain) continue;

                yield return buffer.ToArray();

                buffer.Clear();
            }

            await spawner;
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<GâteauEmballé> ProduireAsync(
        Usine usine,
        [EnumeratorCancellation] CancellationToken token)
    {
        var ligne = new OrdreProduction(usine, token);
        await foreach (var gâteauEmballé in ligne.ProduireAsync().WithCancellation(token))
            yield return gâteauEmballé;
    }
}