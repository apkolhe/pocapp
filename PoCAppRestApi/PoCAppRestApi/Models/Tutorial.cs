using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace PoCAppRestApi.Models
{
    public class Tutorial : TableEntity
    {        
        public int UniqueId { get; set; }
       
        public string Title { get; set; }

        public string Description { get; set; }

        public bool Published { get; set; }
    }
}
