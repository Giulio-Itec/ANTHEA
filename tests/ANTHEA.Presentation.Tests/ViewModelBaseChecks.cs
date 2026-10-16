using Anthea.Presentation.Mvvm;

namespace Anthea.Presentation.Tests;

internal static class ViewModelBaseChecks
{
    internal static int Run()
    {
        var expect = new Expect();
        var model = new Probe();
        var changed = new List<string?>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var token = model.Token;
        expect.That(!model.IsDisposed && !token.IsCancellationRequested, "ViewModel nuovo già chiuso");
        model.Title = "Palificata";
        expect.That(changed.SequenceEqual(["Title"]), "Notifica della proprietà mancante");
        bool cancelledFirst = false;
        token.Register(() => cancelledFirst = model.Released == 0);
        model.Dispose();
        expect.That(model.IsDisposed && token.IsCancellationRequested && model.Token.IsCancellationRequested, "Chiusura senza annullare la vita");
        expect.That(cancelledFirst && model.Released == 1, "Risorse liberate prima dell'annullamento");
        expect.That(changed.Last() == nameof(ViewModelBase.IsDisposed), "Chiusura non notificata");
        model.Dispose();
        expect.That(model.Released == 1, "Seconda chiusura ripetuta");
        expect.Throws<ObjectDisposedException>(model.Use, "Operazione accettata dopo la chiusura");
        // A late completion can still link the token of a closed ViewModel.
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(model.Token))
            expect.That(linked.IsCancellationRequested, "Token della vita inutilizzabile dopo la chiusura");
        return expect.Count;
    }

    private sealed class Probe : ViewModelBase
    {
        private string title = "";
        internal int Released { get; private set; }
        internal CancellationToken Token => Lifetime;
        public string Title { get => title; set => SetProperty(ref title, value); }
        internal void Use() => ThrowIfDisposed();
        protected override void OnDisposed() => Released++;
    }
}
