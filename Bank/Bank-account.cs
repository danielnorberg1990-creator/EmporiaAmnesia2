using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Bank;

/// <summary>
/// Abstrakt basklass för alla kontotyper.
/// Innehåller gemensam logik för saldo, insättningar, uttag, ränta, avgifter,
/// spärrning och överföringar.
/// </summary>
public abstract class BankAccount
{
    /// <summary>
    /// Skapar ett nytt konto efter att ha validerat indata.
    /// </summary>
    /// <param name="accountNumber">Unikt kontonummer.</param>
    /// <param name="accountHolderName">Namn på kontoinnehavaren.</param>
    /// <param name="interestRate">Ränta i procent (måste vara >= 0).</param>
    /// <param name="creditLimit">Kreditgräns i kronor (måste vara >= 0).</param>
    protected BankAccount(
    string accountNumber,
    string accountHolderName,
    decimal interestRate,
    decimal creditLimit)
    {
        // Validera att obligatoriska fält är ifyllda och att belopp är giltiga.
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new ArgumentException("Kontonummer måste anges.", nameof(accountNumber));
        if (string.IsNullOrWhiteSpace(accountHolderName))
            throw new ArgumentException("Kontoinnehavarens namn måste anges.", nameof(accountHolderName));
        if (interestRate < 0)
            throw new ArgumentOutOfRangeException(nameof(interestRate));
        if (creditLimit < 0)
            throw new ArgumentOutOfRangeException(nameof(creditLimit));

        AccountNumber = accountNumber;
        AccountHolderName = accountHolderName;
        InterestRate = interestRate;
        CreditLimit = creditLimit;
    }

    /// <summary>Unikt kontonummer, t.ex. "1001-01".</summary>
    public string AccountNumber { get; }

    /// <summary>Namn på kontoinnehavaren.</summary>
    public string AccountHolderName { get; }

    /// <summary>
    /// Beskrivning av kontotypen ("Lönekonto", "Sparkonto" eller "Kreditkonto").
    /// Implementeras av varje derivat klass.
    /// </summary>
    public abstract string AccountType { get; }

    /// <summary>
    /// Aktuellt saldo. Kan vara negativt upp till kreditgränsen.
    /// Kan bara ändras från inuti klassen.
    /// </summary>
    public decimal Balance { get; private set; }

    /// <summary>Ränta i procent som tillämpas på positivt saldo.</summary>
    public decimal InterestRate { get; }

    /// <summary>Högsta belopp kontot får gå minus med.</summary>
    public decimal CreditLimit { get; }

    /// <summary>
    /// Om kontot är spärrat kan inga transaktioner göras.
    /// Kan bara ändras från inuti klassen.
    /// </summary>
    public bool IsBlocked { get; private set; }

    // ===== Transaktioner =====

    /// <summary>Sätter in pengar på kontot. Kräver ett positivt belopp.</summary>
    public void Deposit(decimal amount)
    {
        EnsureUsable();
        EnsurePositiveAmount(amount);
        Balance += amount;
    }

    /// <summary>
    /// Tar ut pengar från kontot, inom kreditgränsen.
    /// Returnerar true om uttaget lyckades, annars false.
    /// </summary>
    public bool Withdraw(decimal amount)
    {
        EnsureUsable();
        EnsurePositiveAmount(amount);

        // Kontot får inte gå minus med mer än kreditgränsen.
        if (Balance - amount < -CreditLimit)
            return false;

        Balance -= amount;
        return true;
    }

    /// <summary>
    /// Räknar ut och lägger till ränta på det positiva saldot.
    /// Returnerar beloppet som räntade (0 om saldot är <= 0).
    /// </summary>
    public decimal AddInterest()
    {
        EnsureUsable();
        if (Balance <= 0)
            return 0m;

        var interest = Balance * InterestRate / 100m;
        Balance += interest;
        return interest;
    }

    /// <summary>
    /// Drar en kontoavgift ur kontot (fungerar som ett uttag).
    /// Returnerar true om avgiften kunde dras.
    /// </summary>
    public bool ChargeFee(decimal fee)
    {
        return Withdraw(fee);
    }

    // ===== Spärrning =====

    /// <summary>Spärrar kontot så att inga transaktioner kan göras.</summary>
    public void Block()
    {
        IsBlocked = true;
    }

    /// <summary>Ospärrar ett tidigare spärrat konto.</summary>
    public void Unblock()
    {
        IsBlocked = false;
    }

    /// <summary>
    /// Överför pengar till ett annat konto, inom kreditgränsen.
    /// Returnerar true om överföringen lyckades, annars false.
    /// </summary>
    public bool TransferTo(BankAccount recipient, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        if (ReferenceEquals(this, recipient))
            throw new ArgumentException("Mottagaren kan inte vara samma konto.", nameof(recipient));
        EnsureUsable();
        recipient.EnsureUsable();
        EnsurePositiveAmount(amount);

        if (Balance - amount < -CreditLimit)
            return false;

        // Pengarna dras ur avsändaren och läggs på mottagaren.
        Balance -= amount;
        recipient.Balance += amount;
        return true;
    }

    // ===== Information =====

    /// <summary>
    /// Returnerar true om detta konto och mottagar-kontot tillhör samma innehavare.
    /// Jämförelsen är okänslig för gemener/stor bokstäver.
    /// </summary>
    public bool HasSameOwner(BankAccount other)
    {
        return string.Equals(AccountHolderName, other?.AccountHolderName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returnerar en formaterad text med kontots information.</summary>
    public string GetInformation()
    {
        return $"Kontonummer: {AccountNumber}\n" +
               $"Innehavare: {AccountHolderName}\n" +
               $"Kontotyp: {AccountType}\n" +
               $"Saldo: {Balance:C}\n" +
               $"Ränta: {InterestRate:0.##}%\n" +
               $"Kreditgräns: {CreditLimit:C}\n" +
               $"Spärrat: {(IsBlocked ? "Ja" : "Nej")}";
    }

    // ===== Interna valideringshjälpmedel =====

    /// <summary>Kastar undantag om kontot är spärrat.</summary>
    private void EnsureUsable()
    {
        if (IsBlocked)
            throw new InvalidOperationException($"Kontot {AccountNumber} är spärrat.");
    }

    /// <summary>Kastar undantag om beloppet inte är positivt.</summary>
    private static void EnsurePositiveAmount(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Beloppet måste vara större än noll.");
    }
}

// ===== Konkreta kontotyper =====

/// <summary>
/// Lönekonto utan ränta och utan kreditgräns.
/// </summary>
public sealed class SalaryAccount : BankAccount
{
    public SalaryAccount(string accountNumber, string accountHolderName)
        : base(accountNumber, accountHolderName, 0m, 0m)
    {
    }

    public override string AccountType => "Lönekonto";
}

/// <summary>
/// Sparkonto med ränta på insatta pengar och utan kreditgräns.
/// </summary>
public sealed class SavingsAccount : BankAccount
{
    /// <param name="interestRate">Ränta i procent, t.ex. 2.5 för 2,5 %.</param>
    public SavingsAccount(string accountNumber, string accountHolderName, decimal interestRate)
        : base(accountNumber, accountHolderName, interestRate, 0m)
    {
    }

    public override string AccountType => "Sparkonto";
}

/// <summary>
/// Kreditkonto utan ränta, men med en kreditgräns som tillåter minus.
/// </summary>
public sealed class CreditAccount : BankAccount
{
    /// <param name="creditLimit">Högsta belopp kontot får gå minus med, i kronor.</param>
    public CreditAccount(string accountNumber, string accountHolderName, decimal creditLimit)
        : base(accountNumber, accountHolderName, 0m, creditLimit)
    {
    }

    public override string AccountType => "Kreditkonto";
}

/// <summary>
/// Konsolapplikation som låter användaren hantera konton via ett menybaserat gränssnitt.
/// </summary>
public static class Program
{
    /// <summary>Programstart: skapar startkonton och kör menyskärmen i en loop.</summary>
    public static void Main()
    {
        // Startlistan med konton. Användaren kan lägga till och ta bort konton under körning.
        var accounts = new List<BankAccount>
        {
            new SalaryAccount("1001-01", "Anna Andersson"),
            new SavingsAccount("1001-02", "Anna Andersson", 2.5m),
            new CreditAccount("1001-03", "Erik Eriksson", 5_000m)
        };

        // Huvudloopen: visar menyn och sköter val tills användaren väljer "Avsluta".
        while (true)
        {
            PrintMenu();
            var choice = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(choice) || choice == "0")
                break;

            Console.WriteLine();
            try
            {
                // Skickar valet till rätt menyfunktion.
                switch (choice)
                {
                    case "1":
                        PrintBalances(accounts);
                        break;
                    case "2":
                        ShowInformation(accounts);
                        break;
                    case "3":
                        Deposit(accounts);
                        break;
                    case "4":
                        Withdraw(accounts);
                        break;
                    case "5":
                        Transfer(accounts);
                        break;
                    case "6":
                        AddInterest(accounts);
                        break;
                    case "7":
                        ChargeFee(accounts);
                        break;
                    case "8":
                        ChangeBlockedState(accounts, block: true);
                        break;
                    case "9":
                        ChangeBlockedState(accounts, block: false);
                        break;
                    case "10":
                        OpenNewAccount(accounts);
                        break;
                    case "11":
                        DeleteAccount(accounts);
                        break;
                    default:
                        Console.WriteLine("Ogiltigt menyval.");
                        break;
                }
            }
            // Fångar domänrelaterade fel (t.ex. spärrat konto) och ogiltiga belopp
            // så att programmet inte kraschar, utan visar ett tydligt besked.
            catch (InvalidOperationException exception)
            {
                Console.WriteLine($"Nekad: {exception.Message}");
            }
            catch (ArgumentOutOfRangeException exception)
            {
                Console.WriteLine($"Ogiltigt belopp: {exception.Message}");
            }
            // Fångar övriga argumentfel (t.ex. överföring till samma konto) som en sista barriär.
            // Notera: måste ligga efter catch:en för ArgumentOutOfRangeException (som är en underklass).
            catch (ArgumentException exception)
            {
                Console.WriteLine($"Fel: {exception.Message}");
            }

            // Paus så att användaren hinner läsa resultatet innan nästa meny visas.
            Console.WriteLine("\nTryck Enter för att fortsätta.");
            Console.ReadLine();
        }

        Console.WriteLine("Programmet avslutas.");
    }

    // ===== Menyhantering =====

    /// <summary>Renar skärmen och visar huvudmenyn med alla tillgängliga alternativ.</summary>
    private static void PrintMenu()
    {
        Console.Clear();
        Console.WriteLine("=== BANKAPPLIKATION ===");
        Console.WriteLine("1. Visa saldon");
        Console.WriteLine("2. Visa kontoinformation");
        Console.WriteLine("3. Sätt in pengar");
        Console.WriteLine("4. Ta ut pengar");
        Console.WriteLine("5. Överför pengar");
        Console.WriteLine("6. Lägg till ränta");
        Console.WriteLine("7. Ta ut kontoavgift");
        Console.WriteLine("8. Spärra konto");
        Console.WriteLine("9. Ospärra konto");
        Console.WriteLine("10. Öppna nytt konto");
        Console.WriteLine("11. Ta bort konto");
        Console.WriteLine("0. Avsluta");
        Console.Write("\nVälj ett alternativ: ");
    }

    // ===== Menyval 1-9 =====

    /// <summary>Menyval 1: visar alla konton med deras respektive saldo.</summary>
    private static void PrintBalances(List<BankAccount> accounts)
    {
        Console.WriteLine("=== SALDON ===");
        foreach (var account in accounts)
            Console.WriteLine($"{account.AccountNumber} ({account.AccountType}): {account.Balance:C}");
    }

    /// <summary>Menyval 2: visar detaljerad information om ett valt konto.</summary>
    private static void ShowInformation(List<BankAccount> accounts)
    {
        var account = ReadAccount(accounts);
        Console.WriteLine(account.GetInformation());
    }

    /// <summary>Menyval 3: sätter in pengar på ett valt konto.</summary>
    private static void Deposit(List<BankAccount> accounts)
    {
        var account = ReadAccount(accounts);
        var amount = ReadAmount("Belopp att sätta in: ");
        account.Deposit(amount);
        Console.WriteLine($"Insättning genomförd. Nytt saldo: {account.Balance:C}");
    }

    /// <summary>Menyval 4: tar ut pengar från ett valt konto, inom kreditgränsen.</summary>
    private static void Withdraw(List<BankAccount> accounts)
    {
        var account = ReadAccount(accounts);
        var amount = ReadAmount("Belopp att ta ut: ");
        if (!account.Withdraw(amount))
        {
            Console.WriteLine("Uttaget nekades eftersom kreditgränsen eller saldot överskrids.");
            return;
        }

        Console.WriteLine($"Uttag genomfört. Nytt saldo: {account.Balance:C}");
    }

    /// <summary>Menyval 5: överför pengar mellan två valda konton.</summary>
    private static void Transfer(List<BankAccount> accounts)
    {
        Console.WriteLine("Välj konto att överföra från:");
        var sender = ReadAccount(accounts);
        Console.WriteLine("Välj konto att överföra till:");
        var recipient = ReadAccount(accounts);

        // Felhantering: avsändaren och mottagaren får inte vara samma konto.
        // Kontrollen görs här i gränssnittet så att användaren får ett tydligt besked
        // innan beloppet efterfrågas.
        if (ReferenceEquals(sender, recipient))
        {
            Console.WriteLine($"Fel: du kan inte överföra pengar från kontot {sender.AccountNumber} till sig självt. Välj två olika konton.");
            return;
        }

        var amount = ReadAmount("Belopp att överföra: ");

        // Om kontona tillhör olika innehavare, fråga användaren om de vill fortsätta.
        if (!sender.HasSameOwner(recipient) &&
            !ConfirmCrossOwnerTransfer(sender, recipient))
        {
            Console.WriteLine("Överföringen avbröts av användaren.");
            return;
        }

        if (!sender.TransferTo(recipient, amount))
        {
            Console.WriteLine("Överföringen nekades eftersom kreditgränsen eller saldot överskrids.");
            return;
        }

        Console.WriteLine("Överföringen genomfördes.");
    }

    /// <summary>Frågar användaren om att bekräfta en överföring mellan konton med olika innehavare.</summary>
    private static bool ConfirmCrossOwnerTransfer(BankAccount sender, BankAccount recipient)
    {
        Console.WriteLine($"Varning: kontona tillhör olika innehavare.");
        Console.WriteLine($"  {sender.AccountNumber} ({sender.AccountType}) -> {sender.AccountHolderName}");
        Console.WriteLine($"  {recipient.AccountNumber} ({recipient.AccountType}) -> {recipient.AccountHolderName}");
        Console.Write("Vill du ändå överföra? (ja/nej): ");
        var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
        return answer == "ja" || answer == "j";
    }

    /// <summary>Menyval 6: lägger till ränta på ett valt konto.</summary>
    private static void AddInterest(List<BankAccount> accounts)
    {
        var account = ReadAccount(accounts);

        // Kontot måste ha en ränta för att det ska kunna ränta.
        if (account.InterestRate <= 0)
        {
            Console.WriteLine($"Kontot {account.AccountNumber} har ingen ränta.");
            return;
        }

        var interest = account.AddInterest();

        // Ränta räknas bara ut på positivt saldo.
        if (interest <= 0)
        {
            Console.WriteLine("Ingen ränta tillagd eftersom saldot inte är positivt.");
            return;
        }

        Console.WriteLine($"Ränta tillagd: {interest:C}. Nytt saldo: {account.Balance:C}");
    }

    /// <summary>Menyval 7: drar en kontoavgift ur ett valt konto.</summary>
    private static void ChargeFee(List<BankAccount> accounts)
    {
        var account = ReadAccount(accounts);
        var fee = ReadAmount("Avgift: ");
        if (!account.ChargeFee(fee))
        {
            Console.WriteLine("Avgiften kunde inte tas ut.");
            return;
        }

        Console.WriteLine($"Avgiften togs ut. Nytt saldo: {account.Balance:C}");
    }

    /// <summary>Menyval 8/9: spärrar eller ospärrar ett valt konto.</summary>
    private static void ChangeBlockedState(List<BankAccount> accounts, bool block)
    {
        var account = ReadAccount(accounts);
        if (block)
        {
            account.Block();
            Console.WriteLine("Kontot är nu spärrat.");
        }
        else
        {
            account.Unblock();
            Console.WriteLine("Kontot är nu ospärrat.");
        }
    }

    // ===== Menyval 10-11: kontohandtering =====

    /// <summary>Menyval 10: guidar användaren genom att skapa och lägga till ett nytt konto.</summary>
    private static void OpenNewAccount(List<BankAccount> accounts)
    {
        var accountType = ReadAccountType();
        var accountNumber = ReadNewAccountNumber(accounts);
        var accountHolderName = ReadRequiredText("Kontoinnehavarens namn: ");

        // Skapar rätt kontoklass baserat på det valda alternativet.
        // Sparkonto och kreditkonto behöver dessutom ett extra fält.
        BankAccount account = accountType switch
        {
            2 => new SavingsAccount(accountNumber, accountHolderName, ReadNonNegativeDecimal("Ränta (%): ")),
            3 => new CreditAccount(accountNumber, accountHolderName, ReadNonNegativeDecimal("Kreditgräns (kr): ")),
            _ => new SalaryAccount(accountNumber, accountHolderName)
        };

        accounts.Add(account);
        Console.WriteLine($"Kontot {account.AccountNumber} ({account.AccountType}) har öppnats.");
    }

    /// <summary>Menyval 11: tar bort ett valt konto efter bekräftelse från användaren.</summary>
    private static void DeleteAccount(List<BankAccount> accounts)
    {
        // Programmet behöver alltid minst ett konto för att fungera.
        if (accounts.Count <= 1)
        {
            Console.WriteLine("Det går inte att ta bort det sista kontot.");
            return;
        }

        var account = ReadAccount(accounts);

        // Kräver explicit bekräftelse innan kontot raderas.
        Console.WriteLine($"Vill du verkligen ta bort kontot {account.AccountNumber} ({account.AccountHolderName}, saldo {account.Balance:C})?");
        Console.Write("Skriv 'ja' för att bekräfta: ");
        var confirmation = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (confirmation != "ja" && confirmation != "j")
        {
            Console.WriteLine("Kontot togs inte bort.");
            return;
        }

        accounts.Remove(account);
        Console.WriteLine($"Kontot {account.AccountNumber} har tagits bort.");
    }

    // ===== Inmatningshjälpmedel =====

    /// <summary>Läser in ett giltigt kontotypeval (1-3) och returnerar numret.</summary>
    private static int ReadAccountType()
    {
        Console.WriteLine("Välj kontotyp:");
        Console.WriteLine("1. Lönekonto");
        Console.WriteLine("2. Sparkonto");
        Console.WriteLine("3. Kreditkonto");

        // Upprepar tills användaren skriver in ett giltigt val.
        while (true)
        {
            Console.Write("Kontotyp (1-3): ");
            var input = Console.ReadLine();
            if (int.TryParse(input, out var selection) &&
                selection is >= 1 and <= 3)
            {
                return selection;
            }

            Console.WriteLine("Välj 1, 2 eller 3.");
        }
    }

    /// <summary>Läser in ett kontonummer som inte redan är upptaget av ett annat konto.</summary>
    private static string ReadNewAccountNumber(List<BankAccount> accounts)
    {
        while (true)
        {
            var accountNumber = ReadRequiredText("Kontonummer: ");
            // Kontonumret måste vara unikt.
            if (accounts.Any(account => account.AccountNumber == accountNumber))
            {
                Console.WriteLine("Detta kontonummer är redan upptaget. Försök igen.");
                continue;
            }

            return accountNumber;
        }
    }

    /// <summary>Läser in ett obligatoriskt textfält och upprepar tills det är ifyllt.</summary>
    private static string ReadRequiredText(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            var input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
            {
                return input.Trim();
            }

            Console.WriteLine("Värdet får inte vara tomt.");
        }
    }

    /// <summary>
    /// Läser in ett decimalbelopp som är noll eller större.
    /// Hanterar kulturkänslig inmatning (t.ex. 2,5 istället för 2.5).
    /// </summary>
    private static decimal ReadNonNegativeDecimal(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            var input = Console.ReadLine();
            if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out var value) &&
                value >= 0)
            {
                return value;
            }

            Console.WriteLine("Ange ett belopp som är noll eller större, till exempel 2,5 eller 5000.");
        }
    }

    /// <summary>
    /// Visar en lista över tillgängliga konton och låter användaren välja ett.
    /// Upprepar tills ett giltigt val görs.
    /// </summary>
    private static BankAccount ReadAccount(List<BankAccount> accounts)
    {
        Console.WriteLine("Tillgängliga konton:");
        for (var index = 0; index < accounts.Count; index++)
        {
            var account = accounts[index];
            Console.WriteLine($"{index + 1}. {account.AccountNumber} - {account.AccountHolderName} ({account.AccountType})");
        }

        while (true)
        {
            Console.Write("Välj konto (nummer i listan): ");
            var input = Console.ReadLine();
            if (int.TryParse(input, out var selection) &&
                selection >= 1 &&
                selection <= accounts.Count)
            {
                return accounts[selection - 1];
            }

            Console.WriteLine("Välj ett av kontona i listan.");
        }
    }

    /// <summary>
    /// Läser in ett positivt decimalbelopp från användaren.
    /// Hanterar kulturkänslig inmatning (t.ex. 1500,50).
    /// Upprepar tills ett giltigt belopp skrivs in.
    /// </summary>
    private static decimal ReadAmount(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            var input = Console.ReadLine();
            if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) &&
                amount > 0)
            {
                return amount;
            }

            Console.WriteLine("Ange ett positivt belopp, till exempel 1500 eller 1500,50.");
        }
    }
}