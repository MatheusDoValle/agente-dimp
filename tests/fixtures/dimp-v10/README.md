# Fixtures sinteticas menores

20 arquivos sem dados reais de clientes. Latin-1, terminadores e erros sao intencionais;
nao salvar com normalizacao do editor. `.gitattributes` desativa conversao Git de DIMP.
`expected/manifest.json` registra bytes, SHA-256, linhas, escopo e expectativas escritas
a partir de S01/contrato REST. O maior exemplo e somente uma medicao desta amostra;
`productMaxBytes:null` preserva a decisao adiada de tamanho.

`zerado-sintetico` e candidato de arquivo zerado com totais conferidos manualmente:
19 linhas, bloco 0=4, bloco 1=2, bloco 9 incluindo 9999=13 e dez linhas9900. Nao foi certificado
pelo validador oficial. Os demais sao fragmentos ou mutacoes com escopo declarado;
um fragmento de teste fisico nao e um arquivo fiscal valido completo.

SF-001/002/003 possuem variantes que preservam o impedimento para ambas as escolhas.
Nao adotar uma das variantes como valida por conveniencia. Condicoes cobertas inicialmente:
finalidade 4/IND_MOV, campos vazios, versao, hierarquia, datas e decimal. Oraculos para
todas as regras e suas combinacoes ainda pertencem a T029 e aos validadores seguintes.
