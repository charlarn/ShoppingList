﻿using Crawling;
using ShoppingList.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;

namespace ShoppingList.DataAccess
{
    public class DataAccess
    {
        //string connectionString = @"Data Source=(localdb)\ProjectsV13;Initial Catalog = MatkrisDB; Integrated Security = True; Connect Timeout = 30; Encrypt=False;TrustServerCertificate=True;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";
        string connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=MatkrisDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=True;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";

        public static void Seed()
        {

        }

        public string ValidateUser(string email, string password)
        {
            var query = "SELECT Foretagsnamn FROM Foretag WHERE Epost = @epost AND Losenord = @losen";
            string businessname = null;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, conn);
                command.Parameters.Add(new SqlParameter("epost", email));
                command.Parameters.Add(new SqlParameter("losen", password));

                conn.Open();
                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    businessname = reader.GetString(0) ?? null;
                }
                conn.Close();
            }

            return businessname;
        }

        public List<Product> GetTopMatchesByName(string term)
        {
            List<Product> topProducts = new List<Product>();

            var query = "SELECT TOP 5 Produkter.Artikelnummer, min(Produktnamn), min(Pris), min(Jamforelsepris), min(Kategori), " +
                "min(Typ), min(BildURL) FROM Produkter INNER JOIN Priser ON Produkter.Artikelnummer = Priser.Artikelnummer " +
                "WHERE Produktnamn LIKE '%" + @term + "%' GROUP BY Produkter.Artikelnummer";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, conn);
                command.Parameters.Add(new SqlParameter("term", term));

                conn.Open();

                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    topProducts.Add(new Product { Artikelnummer = reader.GetInt32(0), Produktnamn = reader.GetString(1), Pris = reader.GetDecimal(2), Jmf = reader.GetDecimal(3), Kategori = reader.GetString(4), Typ = reader.GetString(5), BildURL = reader.GetString(6) });
                }

                conn.Close();
            }

            return topProducts;
        }

        public void UpdateProductlist(List<string> productlist, string email)
        {
            productlist.RemoveAt(0);

            foreach (var product in productlist)
            {
                var details = product.Split(';');

                var query = "SELECT COUNT(*) FROM Produkter WHERE Artikelnummer = @artnummer";

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    SqlCommand command = new SqlCommand(query, conn);
                    command.Parameters.Add(new SqlParameter("epost", email));
                    command.Parameters.Add(new SqlParameter("artnummer", int.Parse(details[0])));

                    conn.Open();

                    int count = (int)command.ExecuteScalar();

                    if (count > 0)
                    {
                        query = "SELECT COUNT(*) FROM Priser WHERE Foretagsepost = @epost AND Artikelnummer = @artnummer";

                        command = new SqlCommand(query, conn);
                        command.Parameters.Add(new SqlParameter("epost", email));
                        command.Parameters.Add(new SqlParameter("artnummer", int.Parse(details[0])));

                        count = (int)command.ExecuteScalar();

                        if (count > 0)
                        {
                            query = "UPDATE Priser SET Artikelnummer = @artikelnummer, Produktnamn = @produktnamn, Pris = @pris, Jamforelsepris = @jmf, BildURL = @bildURL " +
                                "WHERE Artikelnummer = @artnummer AND Foretagsepost = @epost " +
                                "UPDATE Produkter SET Kategori = @kategori, Typ = @typ, Taggar = @taggar, Mangd = @mangd " +
                                "WHERE Artikelnummer = @artnummer";
                        }
                        else
                        {
                            query = "INSERT INTO Priser (Artikelnummer, Produktnamn, Pris, Jamforelsepris, BildURL, Foretagsepost) " +
                                "VALUES(@artnummer, @produktnamn, @pris, @jmf, @bildURL, @epost) " +
                                "UPDATE Produkter SET Artikelnummer = @artikelnummer, Kategori = @kategori, Typ = @typ, Taggar = @taggar, Mangd = @mangd " +
                                "WHERE Artikelnummer = @artnummer";
                        }
                    }
                    else
                    {
                        query = "INSERT INTO Produkter(Artikelnummer, Kategori, Typ, Taggar, Mangd) " +
                            "VALUES(@artnummer, @kategori, @typ, @taggar, @mangd) " +
                            "INSERT INTO Priser(Artikelnummer, Produktnamn, Pris, Jamforelsepris, BildURL, Foretagsepost) " +
                            "VALUES(@artnummer, @produktnamn, @pris, @jmf, @bildURL, @epost)";
                    }

                    command = new SqlCommand(query, conn);

                    decimal price;
                    decimal jmf;
                    try
                    {
                        string replacePrice = details[2].Replace(',', '.');
                        string replaceJmf = details[3].Replace(',', '.');
                        price = decimal.Parse(replacePrice);
                        jmf = decimal.Parse(replaceJmf);
                    }
                    catch (Exception)
                    {
                        price = decimal.Parse(details[2]);
                        jmf = decimal.Parse(details[3]);
                    }

                    command.Parameters.Add(new SqlParameter("artnummer", details[0]));
                    command.Parameters.Add(new SqlParameter("produktnamn", details[1]));

                    price = price - (price % 0.01m);
                    jmf = jmf - (jmf % 0.01m);

                    command.Parameters.Add(new SqlParameter("artikelnummer", price));
                    command.Parameters.Add(new SqlParameter("pris", price));
                    command.Parameters.Add(new SqlParameter("jmf", jmf));
                    command.Parameters.Add(new SqlParameter("kategori", details[4]));
                    command.Parameters.Add(new SqlParameter("typ", details[5]));
                    command.Parameters.Add(new SqlParameter("bildURL", details[6]));
                    command.Parameters.Add(new SqlParameter("epost", email));
                    command.Parameters.Add(new SqlParameter("taggar", details[7]));
                    var prod = new Product { Produktnamn = details[1] };
                    SetQuantity(prod);
                    command.Parameters.Add(new SqlParameter("mangd", prod.Mangd));
                    command.Parameters.Add(new SqlParameter("mangd", prod.Mangd));

                    command.ExecuteNonQuery();

                    conn.Close();
                }
            }
        }

        public List<Supplier> MatchSuppliersWithProducts(List<Product> checkoutList)
        {
            var query = "SELECT Foretagsnamn, Epost From Foretag";
            List<Supplier> suppliers = new List<Supplier>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, conn);

                conn.Open();
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    suppliers.Add(new Supplier { Name = reader.GetString(0), Email = reader.GetString(1) });
                }

                conn.Close();

                foreach (var product in checkoutList)
                {
                    foreach (var supplier in suppliers)
                    {
                        query =
                            "SELECT Priser.Artikelnummer, Pris, Jamforelsepris, Produktnamn, BildURL FROM Produkter INNER JOIN Priser ON Produkter.Artikelnummer = Priser.Artikelnummer " +
                            "WHERE Produkter.Artikelnummer = @artikelnummer AND Foretagsepost = @email";
                        command = new SqlCommand(query, conn);
                        command.Parameters.Add(new SqlParameter("artikelnummer", product.Artikelnummer));
                        command.Parameters.Add(new SqlParameter("email", supplier.Email));

                        conn.Open();
                        reader = command.ExecuteReader();

                        Product prod;

                        if (reader.Read())
                        {
                            prod = new Product
                            {
                                Artikelnummer = reader.GetInt32(0),
                                Pris = reader.GetDecimal(1),
                                Antal = product.Antal,
                                Jmf = reader.GetDecimal(2),
                                Produktnamn = reader.GetString(3),
                                BildURL = reader.GetString(4),
                                MatchType = MatchType.Match
                            };
                        }
                        else
                        {
                            reader.Close();
                            prod = FindEquivalentProduct(product, supplier.Email, conn);
                        }

                        conn.Close();

                        supplier.Products.Add(prod);
                    }
                }
            }

            return suppliers;
        }

        private Product FindEquivalentProduct(Product product, string supplierEmail, SqlConnection conn)
        {
            var category = product.Kategori;
            var type = product.Typ;

            var query =
                "SELECT TOP 1 Priser.Artikelnummer, Produktnamn, Pris, Jamforelsepris, BildURL FROM Foretag INNER JOIN Priser ON Epost = Foretagsepost " +
                "INNER JOIN Produkter on Priser.Artikelnummer = Produkter.Artikelnummer WHERE Epost = @email AND Typ=@typ";

            SqlCommand command = new SqlCommand(query, conn);

            command.Parameters.Add(new SqlParameter("email", supplierEmail));
            command.Parameters.Add(new SqlParameter("typ", product.Typ));
            SetQuantity(product);
            command.Parameters.Add(new SqlParameter("mangd", product));

            SqlDataReader reader = command.ExecuteReader();

            Product equivalentProduct = null;

            if (reader.Read())
            {
                equivalentProduct = new Product
                {
                    Artikelnummer = reader.GetInt32(0),
                    Produktnamn = reader.GetString(1),
                    Pris = reader.GetDecimal(2),
                    Jmf = reader.GetDecimal(3),
                    BildURL = reader.GetString(4),
                    MatchType = MatchType.Replaced,
                    Replaced = product.Produktnamn,
                    Antal = product.Antal
                };
            }
            else
            {
                equivalentProduct = new Product { Produktnamn = product.Produktnamn, MatchType = MatchType.Unavailable, Antal = product.Antal };
            }

            return equivalentProduct;
        }

        public void SetQuantity(Product product)
        {
            string[] quantityTypes = { "g", "kg", "ml", "l", "cl", "dl", "-p", "st" };
            double value = 0;

            var details = product.Produktnamn.Split(' ').ToList();

            foreach (var type in quantityTypes)
            {
                foreach (var item in details)
                {
                    var index = item.ToLower().IndexOf(type);

                    if (index != -1)
                    {
                        try
                        {
                            if (item.Length != 1 && double.TryParse(item.Substring(index - 1, 1), out value))
                            {
                                if (type == "-p")
                                {
                                    product.MangdUnit = "-pack";
                                    product.Mangd = double.Parse(item.Replace("-pack", "").Replace("-p", "").Replace("ca", ""));
                                }
                                else
                                {
                                    product.MangdUnit = type;
                                    product.Mangd = double.Parse(item.Replace(type, "").Replace("ca", ""));
                                }

                            }
                            else if (details.IndexOf(item) != 0)
                            {
                                if (double.TryParse(details[details.IndexOf(item) - 1], out value))
                                {
                                    product.Mangd = double.Parse(details[details.IndexOf(item) - 1].Replace("ca", ""));
                                    if (type == "-p")
                                        product.MangdUnit = "-pack";
                                    else
                                        product.MangdUnit = type;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        private void FindMatch(string productName)
        {
            var query = "SELECT * FROM Produkter WHERE Kategori = @category";

            List<Product> products = new List<Product>();

            while (products.Any(p => p.Produktnamn.Split(' ').Length > 1))
            {
                foreach (var product in products)
                {
                    if (productName == product.Produktnamn)
                    {
                        return;
                    }
                    else
                    {
                        var splitname = product.Produktnamn.Split(' ').ToList();
                        splitname.Remove(splitname.Last());
                        product.Produktnamn = string.Join(" ", splitname);
                    }
                }

                var namesplit = productName.Split(' ').ToList();
                namesplit.Remove(namesplit.Last());
                productName = string.Join(" ", namesplit);
            }
        }

        public void SaveProducts(List<Product> products, string supplier)
        {
            foreach (var product in products)
            {
                string query = "SELECT COUNT(*) FROM Produkter WHERE Artikelnummer = @artikelnummer";
                int count = 0;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    SqlCommand command = new SqlCommand(query, conn);

                    command.Parameters.Add(new SqlParameter("artikelnummer", product.Artikelnummer));

                    conn.Open();

                    count = (int)command.ExecuteScalar();

                    conn.Close();

                    if (count > 0)
                    {
                        query = "UPDATE Priser SET Produktnamn = @produktnamn, Pris = @pris, JMF = @jmf, Mangd = @mangd, enhet = @enhet " +
                            "WHERE Artikelnummer = @artikelnummer";
                    }
                    else
                    {
                        query = "INSERT INTO Produkter(Artikelnummer, Produktnamn, Pris, JMF, Mangd, Enhet, Foretagsepost) " +
                            "VALUES(@produktID, @produktnamn, @pris, @jmf, @mangd, @enhet, @epost)";
                    }

                    command = new SqlCommand(query, conn);

                    command.Parameters.Add(new SqlParameter("produktID", product.Artikelnummer));
                    command.Parameters.Add(new SqlParameter("produktnamn", product.Produktnamn));
                    command.Parameters.Add(new SqlParameter("pris", product.Pris));
                    command.Parameters.Add(new SqlParameter("jmf", product.Jmf));
                    SetQuantity(product);
                    command.Parameters.Add(new SqlParameter("mangd", product.Mangd));
                    command.Parameters.Add(new SqlParameter("enhet", product.Mangd));
                    if (count == 0)
                        command.Parameters.Add(new SqlParameter("epost", supplier));

                    conn.Open();

                    command.ExecuteNonQuery();

                    conn.Close();
                }
            }
        }
    }
}