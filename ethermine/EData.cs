namespace ethermine
{
    public class EData
    {
        public string worker { get; set; }
        public int time { get; set; }
       
        public int? lastSeen{ get; set; }
        public int reportedHashrate { get; set; }
        public double currentHashrate { get; set; }
        public int validShares { get; set; }
        public int invalidShares { get; set; }
        public int staleShares { get; set; }
        public double averageHashrate { get; set; }
    }
}
