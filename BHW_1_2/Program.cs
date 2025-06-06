using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

public class BankAccount
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Balance { get; set; }

    public BankAccount(int id, string name, int balance)
    {
        Id = id;
        Name = name;
        Balance = balance;
    }

    public BankAccount(int id, string name)
    {
        Id = id;
        Name = name;
        Balance = 0;
    }

    public void UpdateBalance(int amount)
    {
        Balance += amount;
    }

    public void SetBalance(int balance)
    {
        Balance = balance;
    }
}


public class Category
{
    public int Id { get; set; }
    public string Type { get; set; }
    public string Name { get; set; }

    public Category(int id, string type, string name)
    {
        Id = id;
        Type = type;
        Name = name;
    }
}


public class Operation
{
    public int Id { get; set; }
    public string Type { get; set; }
    public int BankAccountId { get; set; }
    public int Amount { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; }
    public int CategoryId { get; set; }

    public Operation(int id, string type, int bankAccountId, int amount, DateTime date, string description, int categoryId)
    {
        Id = id;
        Type = type;
        BankAccountId = bankAccountId;
        Amount = amount;
        Date = date;
        Description = description;
        CategoryId = categoryId;
    }
}


public class BankAccountFacade
{
    private List<BankAccount> accounts = new List<BankAccount>();

    public void CreateAccount(int id, string name)
    {
        var account = new BankAccount(id, name);
        accounts.Add(account);
    }

    public void UpdateAccount(int accountId, string name)
    {
        var account = accounts.Find(a => a.Id == accountId);
        if (account != null)
        {
            accounts.Remove(account);
            accounts.Add(new BankAccount(accountId, name) { Balance = account.Balance });
        }
    }

    public void DeleteAccount(int accountId)
    {
        accounts.RemoveAll(a => a.Id == accountId);
    }

    public BankAccount GetAccount(int accountId)
    {
        return accounts.Find(a => a.Id == accountId);
    }

    public List<BankAccount> GetAllAccounts() => accounts;
}


public class CategoryFacade
{
    private List<Category> categories = new List<Category>();

    public void CreateCategory(int id, string type, string name)
    {
        var category = new Category(id, type, name);
        categories.Add(category);
    }

    public void UpdateCategory(int categoryId, string name)
    {
        var category = categories.Find(c => c.Id == categoryId);
        if (category != null)
        {
            categories.Remove(category);
            categories.Add(new Category(categoryId, category.Type, name));
        }
    }

    public void DeleteCategory(int categoryId)
    {
        categories.RemoveAll(c => c.Id == categoryId);
    }

    public List<Category> GetAllCategories() => categories;
}


public class OperationFacade
{
    private List<Operation> operations = new List<Operation>();
    private readonly BankAccountFacade _accountFacade;

    public OperationFacade(BankAccountFacade accountFacade)
    {
        _accountFacade = accountFacade;
    }

    public void CreateOperation(int id, string type, int bankAccountId, int amount, DateTime date, string description, int categoryId)
    {
        var operation = new Operation(id, type, bankAccountId, amount, date, description, categoryId);
        operations.Add(operation);

        var account = _accountFacade.GetAccount(bankAccountId);
        if (account != null)
            account.UpdateBalance(type == "income" ? amount : -amount);
    }

    public void DeleteOperation(int operationId)
    {
        var operation = operations.Find(o => o.Id == operationId);
        if (operation != null)
        {
            operations.Remove(operation);
            var account = _accountFacade.GetAccount(operation.BankAccountId);
            if (account != null)
                account.UpdateBalance(operation.Type == "income" ? -operation.Amount : operation.Amount);
        }
    }

    public List<Operation> GetAllOperations() => operations;
}


public class AnalyticsFacade
{
    private readonly List<Operation> _operations;

    public AnalyticsFacade(List<Operation> operations)
    {
        _operations = operations;
    }

    public int GetIncomeExpenseDifference(DateTime startDate, DateTime endDate)
    {
        var income = _operations.Where(o => o.Type == "income" && o.Date >= startDate && o.Date <= endDate).Sum(o => o.Amount);
        var expense = _operations.Where(o => o.Type == "expense" && o.Date >= startDate && o.Date <= endDate).Sum(o => o.Amount);
        return income - expense;
    }

    public Dictionary<string, int> GroupByCategory(DateTime startDate, DateTime endDate)
    {
        return _operations
            .Where(o => o.Date >= startDate && o.Date <= endDate)
            .GroupBy(o => o.CategoryId)
            .ToDictionary(g => g.Key.ToString(), g => g.Sum(o => o.Type == "income" ? o.Amount : -o.Amount));
    }
}


public interface ICommand
{
    void Execute();
}

public class AddOperationCommand : ICommand
{
    private Operation operation;

    public AddOperationCommand(Operation operation)
    {
        this.operation = operation;
    }

    public void Execute() { }
}

public class CommandDecorator : ICommand
{
    private ICommand command;

    public CommandDecorator(ICommand command)
    {
        this.command = command;
    }

    public void Execute()
    {
        var stopwatch = Stopwatch.StartNew();
        command.Execute();
        stopwatch.Stop();

        Console.WriteLine($"Execution Time: {stopwatch.ElapsedMilliseconds} ms");
    }
}


public abstract class DataImporter
{
    public void ImportData(string filePath)
    {
        var data = ReadFile(filePath);
        ParseData(data);
        SaveData(data);
    }

    protected abstract string ReadFile(string filePath);

    protected abstract void ParseData(string data);

    protected abstract void SaveData(string parsedData);
}

public class JsonDataImporter : DataImporter
{
    protected override string ReadFile(string filePath)
    {
        return File.ReadAllText(filePath);
    }

    protected override void ParseData(string data) { }

    protected override void SaveData(string parsedData) { }
}


public interface IVisitor
{
    void Visit(BankAccount account);
    void Visit(Category category);
}

public class DataExportVisitor : IVisitor
{
    public void Visit(BankAccount account) { }
    public void Visit(Category category) { }
}


public static class FinancialObjectFactory
{
    public static Operation CreateOperation(int id, string type, int bankAccountId, int amount, DateTime date, string description, int categoryId)
    {
        return new Operation(id, type, bankAccountId, amount, date, description, categoryId);
    }
}


public class InMemoryCacheProxy
{
    private Dictionary<int, BankAccount> cache = new Dictionary<int, BankAccount>();

    public BankAccount GetBankAccount(int id)
    {
        if (!cache.ContainsKey(id))
        {
            var account = LoadFromDatabase(id);
            cache[id] = account;
        }

        return cache[id];
    }

    public void SaveBankAccount(BankAccount account)
    {
        cache[account.Id] = account;
        SaveToDatabase(account);
    }

    private BankAccount LoadFromDatabase(int id)
    {
        return new BankAccount(id, "bank");
    }

    private void SaveToDatabase(BankAccount account) { }
}


class Program
{
    static void Main()
    {
        int bankId = 0;
        int categoryId = 0;
        int operationId = 0;

        var bankAccountFacade = new BankAccountFacade();
        var categoryFacade = new CategoryFacade();
        var operationFacade = new OperationFacade(bankAccountFacade);
        var analyticsFacade = new AnalyticsFacade(operationFacade.GetAllOperations());

        while (true)
        {
            Console.WriteLine(" ");
            Console.WriteLine("1. Добавить счёт");
            Console.WriteLine("2. Редактировать счёт");
            Console.WriteLine("3. Удалить счёт");
            Console.WriteLine("4. Добавить категорию");
            Console.WriteLine("5. Редактировать категорию");
            Console.WriteLine("6. Удалить категорию");
            Console.WriteLine("7. Добавить транзакцию");
            Console.WriteLine("8. Удалить транзакцию");
            Console.WriteLine("0. Выход");
            Console.Write("Выберите действие: ");
            var choice = Console.ReadLine();

            Console.WriteLine(" ");
            try
            {
                switch (choice)
                {
                    case "1":
                        Console.Write("Название счёта: ");
                        var accName = Console.ReadLine();
                        bankAccountFacade.CreateAccount(bankId++, accName);
                        Console.WriteLine($"Счёт {accName} создан. Id: {bankId}\n");
                        break;

                    case "2":
                        Console.Write("ID счёта: ");
                        if (int.TryParse(Console.ReadLine(), out int accIdToEdit))
                        {
                            Console.Write("Новое имя: ");
                            var newName = Console.ReadLine();
                            bankAccountFacade.UpdateAccount(accIdToEdit, newName);
                        }
                        else
                        {
                            Console.WriteLine($"Невереный ввод.\n");
                            break;
                        }
                        break;

                    case "3":
                        Console.Write("ID счёта для удаления: ");
                        if (int.TryParse(Console.ReadLine(), out int accIdDel))
                        {
                            bankAccountFacade.DeleteAccount(accIdDel);
                        }
                        else
                        {
                            Console.WriteLine($"Невереный ввод.\n");
                            break;
                        }
                        break;

                    case "4":
                        Console.Write("Тип категории (income/expense): ");
                        var categoryType = Console.ReadLine();
                        Console.Write("Имя категории: ");
                        var categoryName = Console.ReadLine();
                        categoryFacade.CreateCategory(categoryId++, categoryType, categoryName);
                        Console.WriteLine($"Категория {categoryName} создана. Id {categoryId}\n");
                        break;

                    case "5":
                        Console.Write("ID категории: ");
                        if (int.TryParse(Console.ReadLine(), out int catId))
                        {
                            Console.Write("Новое имя категории: ");
                            var catNewName = Console.ReadLine();
                            categoryFacade.UpdateCategory(catId, catNewName);
                        }
                        else
                        {
                            Console.WriteLine($"Невереный ввод.\n");
                            break;
                        }
                        break;

                    case "6":
                        Console.Write("ID категории для удаления: ");
                        if (int.TryParse(Console.ReadLine(), out int delCatId))
                        {
                            categoryFacade.DeleteCategory(delCatId);
                        }
                        else
                        {
                            Console.WriteLine($"Невереный ввод.\n");
                            break;
                        }
                        break;

                    case "7":
                        Console.Write("ID счёта: ");
                        if (!int.TryParse(Console.ReadLine(), out int opAccId))
                        {
                            Console.WriteLine($"Невереный ввод.\n");
                            break;
                        }

                        Console.Write("Тип (income/expense): ");
                        var opType = Console.ReadLine();

                        Console.Write("Сумма: ");
                        if (!int.TryParse(Console.ReadLine(), out int opAmount))
                        {
                            Console.WriteLine($"Невереный ввод.\n");
                            break;
                        }

                        Console.Write("Описание: ");
                        var opDesc = Console.ReadLine();

                        Console.Write("Дата (dd.MM.yyyy): ");
                        var dateStr = Console.ReadLine();
                        DateTime.TryParseExact(dateStr, new[] { "dd.MM.yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var opDate);


                        Console.Write("ID категории: ");
                        if (!int.TryParse(Console.ReadLine(), out int opCatId))
                        {
                            Console.WriteLine($"Невереный ввод.\n");
                            break;
                        }

                        operationFacade.CreateOperation(operationId++, opType, opAccId, opAmount, opDate, opDesc, opCatId);
                        Console.WriteLine($"Транзакция произведена. Id: {operationId}\n");
                        break;

                    case "8":
                        Console.Write("ID операции для удаления: ");
                        if (int.TryParse(Console.ReadLine(), out int delOpId))
                        {
                            operationFacade.DeleteOperation(delOpId);
                        }
                        else
                        {
                            Console.WriteLine($"Невереный ввод.\n");
                            break;
                        }
                        break;

                    case "0":
                        return;

                    default:
                        Console.WriteLine("Неверный выбор\n");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка.\n");
            }
        }
    }
}
