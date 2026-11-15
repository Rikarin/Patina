from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.stack_checks import StackChecks
from ...types import UNSET, Unset
from typing import cast



def _get_kwargs(
    id: str,
    *,
    preset: str | Unset = UNSET,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["preset"] = preset


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/stacks/{id}/checks".format(id=quote(str(id), safe=""),),
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | StackChecks | None:
    if response.status_code == 200:
        response_200 = StackChecks.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 422:
        response_422 = ProblemDetails.from_dict(response.json())



        return response_422

    if response.status_code == 503:
        response_503 = ProblemDetails.from_dict(response.json())



        return response_503

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails | StackChecks]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    preset: str | Unset = UNSET,

) -> Response[ProblemDetails | StackChecks]:
    """ Checks the texture set before export, against its project's budget, size limit, texel density and
    export preset, and its layout and values against what a mesh and a physical material can have. Each
    problem has a stable code and where it is. A preset checks it for that platform, with the limits the
    project keeps for it. The export preset of a platform to check for, or none for the project's own.

    Args:
        id (str):
        preset (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackChecks]
     """


    kwargs = _get_kwargs(
        id=id,
preset=preset,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    preset: str | Unset = UNSET,

) -> ProblemDetails | StackChecks | None:
    """ Checks the texture set before export, against its project's budget, size limit, texel density and
    export preset, and its layout and values against what a mesh and a physical material can have. Each
    problem has a stable code and where it is. A preset checks it for that platform, with the limits the
    project keeps for it. The export preset of a platform to check for, or none for the project's own.

    Args:
        id (str):
        preset (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackChecks
     """


    return sync_detailed(
        id=id,
client=client,
preset=preset,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    preset: str | Unset = UNSET,

) -> Response[ProblemDetails | StackChecks]:
    """ Checks the texture set before export, against its project's budget, size limit, texel density and
    export preset, and its layout and values against what a mesh and a physical material can have. Each
    problem has a stable code and where it is. A preset checks it for that platform, with the limits the
    project keeps for it. The export preset of a platform to check for, or none for the project's own.

    Args:
        id (str):
        preset (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackChecks]
     """


    kwargs = _get_kwargs(
        id=id,
preset=preset,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    preset: str | Unset = UNSET,

) -> ProblemDetails | StackChecks | None:
    """ Checks the texture set before export, against its project's budget, size limit, texel density and
    export preset, and its layout and values against what a mesh and a physical material can have. Each
    problem has a stable code and where it is. A preset checks it for that platform, with the limits the
    project keeps for it. The export preset of a platform to check for, or none for the project's own.

    Args:
        id (str):
        preset (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackChecks
     """


    return (await asyncio_detailed(
        id=id,
client=client,
preset=preset,

    )).parsed
