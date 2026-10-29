const memoryDatabase = db.getSiblingDB("agente_memory");
memoryDatabase.createUser({
  user: "agente_memory",
  pwd: process.env.AGENT_MEMORY_PASSWORD,
  roles: [{ role: "readWrite", db: "agente_memory" }]
});
