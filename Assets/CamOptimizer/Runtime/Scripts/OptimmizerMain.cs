using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OptimmizerMain : MonoBehaviour
{
    public enum OptimizeMethod
    {
        GeneticAlgorithm,
        ParticleSwarmOpimization
    }

    public OptimizeMethod opt_method = OptimizeMethod.ParticleSwarmOpimization;

    PSO_optimizer pso_optim;
    GA_optimizer ga_optim;
    // Start is called before the first frame update
    void Start()
    {
        pso_optim = GetComponent<PSO_optimizer>();
        ga_optim = GetComponent<GA_optimizer>();
    }

    // Update is called once per frame
    void Update()
    {
        switch (opt_method)
        {
            case OptimizeMethod.GeneticAlgorithm:
                ga_optim.iterate();
                break;
            case OptimizeMethod.ParticleSwarmOpimization:
                pso_optim.iterate();
                break;
        }
    }
}
