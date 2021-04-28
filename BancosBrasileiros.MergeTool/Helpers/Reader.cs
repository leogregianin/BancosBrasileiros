﻿// ***********************************************************************
// Assembly         : BancosBrasileiros.MergeTool
// Author           : Guilherme Branco Stracini
// Created          : 05-19-2020
//
// Last Modified By : Guilherme Branco Stracini
// Last Modified On : 04-27-2021
// ***********************************************************************
// <copyright file="Reader.cs" company="Guilherme Branco Stracini ME">
//     Copyright (c) Guilherme Branco Stracini ME. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace BancosBrasileiros.MergeTool.Helpers
{
    using Dto;
    using iTextSharp.text.pdf;
    using iTextSharp.text.pdf.parser;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Class Reader.
    /// </summary>
    internal class Reader
    {
        /// <summary>
        /// The CSV pattern
        /// </summary>
        private readonly Regex _csvPattern = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// The SLC pattern
        /// </summary>
        private readonly Regex _slcPattern = new Regex(@"^(?<code>\d{1,3})\s(?<cnpj>\d{2}\.\d{3}\.\d{3}\/\d{4}([-|·|\.|\s]{1,2})\d{2})\s(?<nome>.+?)(?:[\s|X]){1,7}$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// The counting SLC
        /// </summary>
        private int _countingSlc;

        public List<Bank> LoadStr()
        {
            var result = new List<Bank>();


            using var webClient = new WebClient { Encoding = Encoding.UTF8 };

            var data = webClient.DownloadString("http://www.bcb.gov.br/pom/spb/estatistica/port/ParticipantesSTRport.csv");
            var lines = data.Split("\n").Skip(1).ToArray();

            result.AddRange(lines
                .Select(line => _csvPattern.Split(line))
                .Where(columns => columns.Length > 1 && int.TryParse(columns[2], out _))
                .Select(columns => new Bank
                {
                    CompeString = columns[2],
                    IspbString = columns[0],
                    LongName = columns[5].Trim(),
                    ShortName = columns[1].Trim(),
                    DateOperationStarted = DateTime.ParseExact(columns[6].Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd"),
                    Network = columns[4]
                }));

            return result;
        }

        /// <summary>
        /// Loads the CNPJ.
        /// </summary>
        /// <returns>List&lt;Bank&gt;.</returns>
        public List<Bank> LoadCnpj()
        {
            var result = new List<Bank>();
            var lines = File.ReadAllLines("data\\bancos-cnpj.md").Skip(4).ToArray();
            foreach (var line in lines)
            {
                var columns = line.Split("|");
                for (var l = 0; l < columns.Length; l++)
                    columns[l] = columns[l].Trim();

                var bank = new Bank
                {
                    Document = columns[0],
                    LongName = columns[1],
                    Type = columns[2],
                    Url = columns[3]
                };
                result.Add(bank);
            }
            return result;
        }

        /// <summary>
        /// Loads the site.
        /// </summary>
        /// <returns>List&lt;Bank&gt;.</returns>
        public List<Bank> LoadSite()
        {
            var result = new List<Bank>();
            var lines = File.ReadAllLines("data\\bancos-site.md").Skip(4).ToArray();
            foreach (var line in lines)
            {
                var columns = line.Split("|");
                for (var l = 0; l < columns.Length; l++)
                    columns[l] = columns[l].Trim();

                var bank = new Bank
                {
                    Compe = int.Parse(columns[0]),
                    LongName = columns[1],
                    Url = columns[2]
                };
                result.Add(bank);
            }

            return result;
        }

        /// <summary>
        /// Loads the SLC.
        /// </summary>
        /// <returns>List&lt;Bank&gt;.</returns>
        public List<Bank> LoadSlc()
        {
            _countingSlc = 0;
            var result = new List<Bank>();

            var reader = new PdfReader("https://www.cip-bancos.org.br/Monitoramento/Participantes%20Homologados.pdf");
            for (var currentPage = 1; currentPage <= reader.NumberOfPages; currentPage++)
            {
                var currentText =
                    PdfTextExtractor.GetTextFromPage(reader, currentPage, new SimpleTextExtractionStrategy());
                currentText = Encoding.UTF8.GetString(Encoding.Convert(
                    Encoding.Default,
                    Encoding.UTF8,
                    Encoding.Default.GetBytes(currentText)));
                result.AddRange(ParseLines(currentText));
            }

            return result;
        }

        /// <summary>
        /// Parses the lines.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns>IEnumerable&lt;Bank&gt;.</returns>
        private IEnumerable<Bank> ParseLines(string page)
        {
            var result = new List<Bank>();
            var lines = page.Split("\n");

            var spliced = string.Empty;

            foreach (var line in lines)
            {
                if (!_slcPattern.IsMatch(line))
                {
                    spliced += $" {line}";
                    continue;
                }

                Bank bank;

                if (!string.IsNullOrWhiteSpace(spliced))
                {
                    bank = ParseLine(spliced.Trim());

                    if (bank != null)
                        result.Add(bank);

                    spliced = string.Empty;
                }

                bank = ParseLine(line);

                if (bank != null)
                    result.Add(bank);
            }

            return result;
        }

        private Bank ParseLine(string line)
        {
            if (!_slcPattern.IsMatch(line))
                return null;

            var match = _slcPattern.Match(line);

            var code = Convert.ToInt32(match.Groups["code"].Value.Trim());

            _countingSlc++;

            if (_countingSlc != code)
                Console.WriteLine($"SLC | Counting: {_countingSlc} | Code: {code}");

            return new Bank
            {
                Document = match.Groups["cnpj"].Value.Trim(),
                LongName = match.Groups["nome"].Value.Trim()
            };
        }
    }
}